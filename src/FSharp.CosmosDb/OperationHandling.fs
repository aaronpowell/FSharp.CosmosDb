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
    | None -> "id"

let getPartitionKeyValue<'T> (single: 'T) =
    let partitionKey =
        PartitionKeyAttributeTools.findPartitionKey<'T> ()

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
    let result =
        execQueryInternal getClient op (QueryRequestOptions())

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
                    |> List.map
                        (fun single ->
                            let pk = getPartitionKeyValue single

                            container.CreateItemAsync(single, pk)
                            |> Async.AwaitTask)
        }

    match result with
    | Some result ->
        result
        |> List.map
            (fun item ->
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
                    |> List.map
                        (fun single ->
                            let pk = getPartitionKeyValue single

                            container.UpsertItemAsync(single, pk)
                            |> Async.AwaitTask)
        }

    match result with
    | Some result ->
        result
        |> List.map
            (fun item ->
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
    
let execCreateContainer (getClient: ConnectionOperation -> CosmosClient) (op: CreateContainerOp<'T>) =
    let connInfo = op.Connection
    let client = getClient connInfo

    let result =
        maybe {
            let! databaseId = connInfo.DatabaseId
            let! containerName = connInfo.ContainerName

            let db = client.GetDatabase databaseId

            let containerCreation = db.CreateContainerAsync(containerName, "id")

            return
                containerCreation
                |> Async.AwaitTask
        }

    match result with
    | Some result -> result
    | None -> failwith "Unable to create container"
    
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
                container.DeleteContainerAsync ()
                |> Async.AwaitTask
        }

    match result with
    | Some result -> result
    | None -> failwith "Unable to delete container"
    
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
