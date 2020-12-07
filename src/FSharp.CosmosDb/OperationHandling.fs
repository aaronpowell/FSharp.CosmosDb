[<RequireQualifiedAccess>]
module internal OperationHandling

open FSharp.CosmosDb
open Azure.Cosmos
open FSharp.Control
open System

let execQueryInternal (getClient: ConnectionOperation -> CosmosClient) (op: QueryOp<'T>) =
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
            |> List.fold (fun (qd: QueryDefinition) (key, value) -> qd.WithParameter(key, value))
                   (QueryDefinition query)

        return container.GetItemQueryIterator<'T> qd
    }

let execQuery (getClient: ConnectionOperation -> CosmosClient) (op: QueryOp<'T>) =
    let result = execQueryInternal getClient op

    match result with
    | Some result -> result |> AsyncSeq.ofAsyncEnum
    | None ->
        failwith "Unable to construct a query as some values are missing across the database, container name and query"

let execQueryBatch (getClient: ConnectionOperation -> CosmosClient) (op: QueryOp<'T>) =
    let result = execQueryInternal getClient op

    match result with
    | Some result -> result.AsPages() |> AsyncSeq.ofAsyncEnum
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

            let partitionKey =
                PartitionKeyAttributeTools.findPartitionKey<'T> ()

            let getPartitionKeyValue single =
                match partitionKey with
                | Some propertyInfo ->
                    let value = propertyInfo.GetValue(single)
                    Nullable(PartitionKey(value.ToString()))
                | None -> Nullable()

            return match op.Values with
                   | [ single ] ->
                       let pk = getPartitionKeyValue single
                       [ container.CreateItemAsync<'T>(single, pk)
                         |> Async.AwaitTask ]
                   | _ ->
                       op.Values
                       |> List.map (fun single ->
                           let pk = getPartitionKeyValue single
                           container.CreateItemAsync<'T>(single, pk)
                           |> Async.AwaitTask)
        }

    match result with
    | Some result ->
        result
        |> List.map (fun item ->
            async {
                let! value = item
                return value.Value
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

            let partitionKey =
                PartitionKeyAttributeTools.findPartitionKey<'T> ()

            let getPartitionKeyValue single =
                match partitionKey with
                | Some propertyInfo ->
                    let value = propertyInfo.GetValue(single)
                    Nullable(PartitionKey(value.ToString()))
                | None -> Nullable()

            return match op.Values with
                   | [ single ] ->
                       let pk = getPartitionKeyValue single
                       [ container.UpsertItemAsync<'T>(single, pk)
                         |> Async.AwaitTask ]
                   | _ ->
                       op.Values
                       |> List.map (fun single ->
                           let pk = getPartitionKeyValue single
                           container.UpsertItemAsync<'T>(single, pk)
                           |> Async.AwaitTask)
        }

    match result with
    | Some result ->
        result
        |> List.map (fun item ->
            async {
                let! value = item
                return value.Value
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

            return (container,
                    container.ReadItemAsync(op.Id, PartitionKey op.PartitionKey)
                    |> Async.AwaitTask)
        }

    match result with
    | Some (container, result) ->
        [ async {
            let! currentItemResponse = result

            let newItem = op.Updater currentItemResponse.Value

            let partitionKey =
                PartitionKeyAttributeTools.findPartitionKey<'T> ()

            let pk =
                match partitionKey with
                | Some propertyInfo ->
                    let value = propertyInfo.GetValue(newItem)
                    Nullable(PartitionKey(value.ToString()))
                | None -> Nullable()

            let! newItemResponse =
                container.ReplaceItemAsync(newItem, op.Id, pk)
                |> Async.AwaitTask

            return newItemResponse.Value
          } ]
        |> AsyncSeq.ofSeqAsync

    | None -> failwith "Unable to read from the container to get the item for updating"

let execDelete (getClient: ConnectionOperation -> CosmosClient) (op: DeleteOp<'T>) =
    let connInfo = op.Connection
    let client = getClient connInfo

    let result =
        maybe {
            let! databaseId = connInfo.DatabaseId
            let! containerName = connInfo.ContainerName

            let db = client.GetDatabase databaseId

            let container = db.GetContainer containerName

            return container.DeleteItemAsync(op.Id, PartitionKey op.PartitionKey)
                   |> Async.AwaitTask
        }

    match result with
    | Some result ->
        [ async {
            let! currentItemResponse = result
            return currentItemResponse.Value
          } ]
        |> AsyncSeq.ofSeqAsync

    | None -> failwith "Unable to read from the container to get the item for updating"
