[<RequireQualifiedAccess>]
module internal OperationHandling

open FSharp.CosmosDb
open Microsoft.Azure.Cosmos
open FSharp.Control
open System

let getIdFieldName<'T> (item: 'T) =
    let idAttr = IdAttributeTools.findId<'T> ()

    match idAttr with
    | Some attr -> attr.GetValue(item).ToString()
    | None ->
        let property = item.GetType().GetProperties() |> Array.tryFind(fun p -> p.Name.Equals("id", StringComparison.InvariantCultureIgnoreCase))
        match property with
        | Some property -> property.GetValue(item).ToString()
        | None -> failwith "Unable to determine the id field of the type. Either use the IdAttribute or have a field named Id"

let getPartitionKeyValue<'T> (single: 'T) =
    let partitionKey = PartitionKeyAttributeTools.findPartitionKey<'T> ()

    match partitionKey with
    | Some propertyInfo ->
        let value = propertyInfo.GetValue(single)
        Nullable(PartitionKey(value.ToString()))
    | None -> Nullable()

let execQueryInternal
    (getClient: ConnectionOperation -> CosmosClient)
    (op: QueryOp<'T>)
    (queryOps: QueryRequestOptions)
    =
    let connInfo = op.Connection
    let client = getClient connInfo

    maybe {
        let! databaseId = connInfo.DatabaseId
        let! containerName = connInfo.ContainerName

        let db = client.GetDatabase databaseId

        let container = db.GetContainer containerName

        let! query = op.Query

        let qd =
            op.Parameters
            |> List.fold
                (fun (qd: QueryDefinition) (key, value) -> qd.WithParameter(key, value))
                (QueryDefinition query)

        return container.GetItemQueryIterator<'T>(qd, null, queryOps)
    }

let execQuery (getClient: ConnectionOperation -> CosmosClient) (op: QueryOp<'T>) =
    let result = execQueryInternal getClient op (QueryRequestOptions())

    match result with
    | Some result -> result |> AsyncSeq.ofAsyncFeedIterator
    | None ->
        failwith "Unable to construct a query as some values are missing across the database, container name and query"

let execQueryBatch (getClient: ConnectionOperation -> CosmosClient) (op: QueryOp<'T>) (queryOps: QueryRequestOptions) =
    let result = execQueryInternal getClient op queryOps

    match result with
    | Some result -> result |> AsyncSeq.ofAsyncFeedIterator
    | None ->
        failwith "Unable to construct a query as some values are missing across the database, container name and query"

let execCheckIfDatabaseExists (getClient: ConnectionOperation -> CosmosClient) (op: CheckIfDatabaseExistsOp) =
    let connInfo = op.Connection
    let client = getClient connInfo

    use iterator = client.GetDatabaseQueryIterator<DatabaseProperties>()

    match connInfo.DatabaseId with
    | Some databaseId ->
        iterator
        |> AsyncSeq.unfold (fun t ->
            if iterator.HasMoreResults then
                Some(iterator.ReadNextAsync(), iterator)
            else
                None)
        |> AsyncSeq.collect (fun i ->
            asyncSeq {
                let! c = i |> Async.AwaitTask

                for x in c do
                    yield x
            })
        |> AsyncSeq.exists (fun i -> i.Id = databaseId)
    | None -> failwith "failed to check if database exists"

let execInsert (getClient: ConnectionOperation -> CosmosClient) (op: InsertOp<'T>) =
    let connInfo = op.Connection
    let client = getClient connInfo

    let result =
        maybe {
            let! databaseId = connInfo.DatabaseId
            let! containerName = connInfo.ContainerName

            let db = client.GetDatabase databaseId

            let container = db.GetContainer containerName

            return
                match op.Values with
                | [ single ] ->
                    let pk = getPartitionKeyValue single

                    [ container.CreateItemAsync(single, pk)
                      |> Async.AwaitTask ]
                | _ ->
                    op.Values
                    |> List.map (fun single ->
                        let pk = getPartitionKeyValue single

                        container.CreateItemAsync(single, pk)
                        |> Async.AwaitTask)
        }

    match result with
    | Some result ->
        result
        |> List.map (fun item ->
            async {
                let! value = item
                return value.Resource
            })
        |> AsyncSeq.ofSeqAsync
    | None ->
        failwith "Unable to construct a query as some values are missing across the database, container name and query"

let execUpsert (getClient: ConnectionOperation -> CosmosClient) (op: UpsertOp<'T>) =
    let connInfo = op.Connection
    let client = getClient connInfo

    let result =
        maybe {
            let! databaseId = connInfo.DatabaseId
            let! containerName = connInfo.ContainerName

            let db = client.GetDatabase databaseId

            let container = db.GetContainer containerName

            return
                match op.Values with
                | [ single ] ->
                    let pk = getPartitionKeyValue single

                    [ container.UpsertItemAsync(single, pk)
                      |> Async.AwaitTask ]
                | _ ->
                    op.Values
                    |> List.map (fun single ->
                        let pk = getPartitionKeyValue single

                        container.UpsertItemAsync(single, pk)
                        |> Async.AwaitTask)
        }

    match result with
    | Some result ->
        result
        |> List.map (fun item ->
            async {
                let! value = item
                return value.Resource
            })
        |> AsyncSeq.ofSeqAsync
    | None ->
        failwith "Unable to construct a query as some values are missing across the database, container name and query"

let execUpdate (getClient: ConnectionOperation -> CosmosClient) (op: UpdateOp<'T>) =
    let connInfo = op.Connection
    let client = getClient connInfo

    let result =
        maybe {
            let! databaseId = connInfo.DatabaseId
            let! containerName = connInfo.ContainerName

            let db = client.GetDatabase databaseId

            let container = db.GetContainer containerName

            return
                (container,
                 container.ReadItemAsync(op.Id, PartitionKey op.PartitionKey)
                 |> Async.AwaitTask)
        }

    match result with
    | Some (container, result) ->
        [ async {
              let! currentItemResponse = result

              let newItem = op.Updater currentItemResponse.Resource

              let! newItemResponse =
                  container.ReplaceItemAsync(newItem, op.Id, getPartitionKeyValue newItem)
                  |> Async.AwaitTask

              return newItemResponse.Resource
          } ]
        |> AsyncSeq.ofSeqAsync

    | None -> failwith "Unable to read from the container to get the item for updating"

let execDeleteItem (getClient: ConnectionOperation -> CosmosClient) (op: DeleteItemOp<'T>) =
    let connInfo = op.Connection
    let client = getClient connInfo

    let result =
        maybe {
            let! databaseId = connInfo.DatabaseId
            let! containerName = connInfo.ContainerName

            let db = client.GetDatabase databaseId

            let container = db.GetContainer containerName

            return
                container.DeleteItemAsync(op.Id, PartitionKey op.PartitionKey)
                |> Async.AwaitTask
        }

    match result with
    | Some result ->
        [ async {
              let! currentItemResponse = result
              return currentItemResponse.Resource
          } ]
        |> AsyncSeq.ofSeqAsync

    | None -> failwith "Unable to read from the container to get the item for updating"

let execGetContainerProperties (getClient: ConnectionOperation -> CosmosClient) (op: GetContainerPropertiesOp) =
    let connInfo = op.Connection
    let client = getClient connInfo

    let containerName =
        match connInfo.ContainerName with
        | None -> failwith "ContainerName is not provided"
        | Some containerName -> containerName

    use iterator =
        match connInfo.DatabaseId with
        | None -> failwith "DatabaseId is not provided"
        | Some databaseId ->
            client
                .GetDatabase(databaseId)
                .GetContainerQueryIterator<ContainerProperties>()

    iterator
    |> AsyncSeq.unfold (fun t ->
        if iterator.HasMoreResults then
            Some(iterator.ReadNextAsync(), iterator)
        else
            None)
    |> AsyncSeq.collect (fun i ->
        asyncSeq {
            let! c = i |> Async.AwaitTask

            for x in c do
                yield x
        })
    |> AsyncSeq.tryFind (fun i -> i.Id = containerName)

let execCheckIfContainerExists (getClient: ConnectionOperation -> CosmosClient) (op: CheckIfContainerExistsOp) =
    async {
        let! containerProperties = execGetContainerProperties getClient { Connection = op.Connection }

        return containerProperties |> Option.isSome
    }

let execDeleteContainer (getClient: ConnectionOperation -> CosmosClient) (op: DeleteContainerOp<'T>) =
    let connInfo = op.Connection
    let client = getClient connInfo

    let result =
        maybe {
            let! databaseId = connInfo.DatabaseId
            let! containerName = connInfo.ContainerName

            let db = client.GetDatabase databaseId

            let container = db.GetContainer containerName

            return
                container.DeleteContainerAsync()
                |> Async.AwaitTask
        }

    match result with
    | Some result -> result
    | None -> failwith "Unable to delete container"

let execDeleteContainerIfExists (getClient: ConnectionOperation -> CosmosClient) (op: DeleteContainerIfExistsOp) =
    async {
        let! databaseExists = execCheckIfDatabaseExists getClient { Connection = op.Connection }
        let! containerExists = execCheckIfContainerExists getClient { Connection = op.Connection }

        if databaseExists && containerExists then
            do!
                execDeleteContainer getClient { Connection = op.Connection }
                |> Async.Ignore
    }

let execRead (getClient: ConnectionOperation -> CosmosClient) (op: ReadOp<'T>) =
    let connInfo = op.Connection
    let client = getClient connInfo

    let result =
        maybe {
            let! databaseId = connInfo.DatabaseId
            let! containerName = connInfo.ContainerName

            let db = client.GetDatabase databaseId
            let container = db.GetContainer containerName

            return
                container.ReadItemAsync(op.Id, PartitionKey op.PartitionKey)
                |> Async.AwaitTask
        }

    match result with
    | Some result ->
        [ async {
              let! currentItemResponse = result
              return currentItemResponse.Resource
          } ]
        |> AsyncSeq.ofSeqAsync
    | None -> failwith "Unable to read from the container to get item"

let execReplace (getClient: ConnectionOperation -> CosmosClient) (op: ReplaceOp<'T>) =
    let connInfo = op.Connection
    let client = getClient connInfo

    let result =
        maybe {
            let! databaseId = connInfo.DatabaseId
            let! containerName = connInfo.ContainerName

            let db = client.GetDatabase databaseId
            let container = db.GetContainer containerName

            let item = op.Item

            return
                container.ReplaceItemAsync(op.Item, getIdFieldName item, getPartitionKeyValue item)
                |> Async.AwaitTask
        }

    match result with
    | Some result ->
        [ async {
              let! currentItemResponse = result
              return currentItemResponse.Resource
          } ]
        |> AsyncSeq.ofSeqAsync
    | None -> failwith "Unable to read from the container to replace item"

let execCreateContainer (getClient: ConnectionOperation -> CosmosClient) (op: CreateContainerOp<'T>) =
    let connInfo = op.Connection
    let client = getClient connInfo

    let result =
        maybe {
            let! databaseId = connInfo.DatabaseId
            let! containerName = connInfo.ContainerName

            let db = client.GetDatabase databaseId

            let! partitionKey = PartitionKeyAttributeTools.findPartitionKey<'T> ()

            let properties = ContainerProperties(containerName, sprintf "/%s" partitionKey.Name)

            return
                db.CreateContainerAsync properties
                |> Async.AwaitTask
        }

    match result with
    | Some result ->
        [ async {
              let! res = result
              return res.Resource
          } ]
        |> AsyncSeq.ofSeqAsync
    | None ->
        failwith
            "Unable to create the container. Ensure the name is valid and there is a partition key defined on the type"

let execCreateContainerIfNotExists
    (getClient: ConnectionOperation -> CosmosClient)
    (op: CreateContainerIfNotExistsOp<'T>)
    =
    let connInfo = op.Connection
    let client = getClient connInfo

    let result =
        maybe {
            let! databaseId = connInfo.DatabaseId
            let! containerName = connInfo.ContainerName

            let db = client.GetDatabase databaseId

            let! partitionKey = PartitionKeyAttributeTools.findPartitionKey<'T> ()

            let properties = ContainerProperties(containerName, sprintf "/%s" partitionKey.Name)

            return
                db.CreateContainerIfNotExistsAsync properties
                |> Async.AwaitTask
        }

    match result with
    | Some result ->
        [ async {
              let! res = result
              return res.Resource
          } ]
        |> AsyncSeq.ofSeqAsync
    | None ->
        failwith
            "Unable to create the container. Ensure the name is valid and there is a partition key defined on the type"

let execCreateDatabase (getClient: ConnectionOperation -> CosmosClient) (op: CreateDatabaseOp) =
    let connInfo = op.Connection
    let client = getClient connInfo

    let result =
        maybe {
            let! databaseId = connInfo.DatabaseId

            return
                client.CreateDatabaseAsync databaseId
                |> Async.AwaitTask
        }

    match result with
    | Some result ->
        [ async {
              let! res = result
              return res.Resource
          } ]
        |> AsyncSeq.ofSeqAsync
    | None ->
        failwith
            "Unable to create the database. Ensure the database does not already exist and that your connection info is valid"

let execCreateDatabaseIfNotExists (getClient: ConnectionOperation -> CosmosClient) (op: CreateDatabaseIfNotExistsOp) =
    let connInfo = op.Connection
    let client = getClient connInfo

    let result =
        maybe {
            let! databaseId = connInfo.DatabaseId

            return
                client.CreateDatabaseIfNotExistsAsync databaseId
                |> Async.AwaitTask
        }

    match result with
    | Some result ->
        [ async {
              let! res = result
              return res.Resource
          } ]
        |> AsyncSeq.ofSeqAsync
    | None ->
        failwith
            "Unable to create the database. Ensure the database does not already exist and that your connection info is valid"
