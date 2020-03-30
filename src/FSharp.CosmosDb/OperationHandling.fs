[<RequireQualifiedAccess>]
module internal OperationHandling

open FSharp.CosmosDb
open Azure.Cosmos
open FSharp.Control
open System

let execQuery (getClient: ConnectionOperation -> CosmosClient) (op: QueryOp) =
    let connInfo = op.Connection
    let client = getClient connInfo

    let result =
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

            return container.GetItemQueryIterator<'T> qd |> AsyncSeq.ofAsyncEnum
        }

    match result with
    | Some result -> result
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
                match PartitionKeyAttributeTools.findPartitionKey<'T>() with
                | Some name -> Nullable(PartitionKey name)
                | None -> Nullable()

            return match op.Values with
                   | [ single ] -> [ container.CreateItemAsync<'T>(single, partitionKey) |> Async.AwaitTask ]
                   | _ ->
                       op.Values
                       |> List.map
                           (fun single -> container.CreateItemAsync<'T>(single, partitionKey) |> Async.AwaitTask)
        }

    match result with
    | Some result ->
        result
        |> List.map (fun item ->
            async {
                let! value = item
                return value.Value })
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

            let partitionKey =
                match PartitionKeyAttributeTools.findPartitionKey<'T>() with
                | Some name -> PartitionKey name
                | None ->
                    failwith
                        "Unable to determine partition key from type, ensure there is a [<PartitionKey>] attribute on the apprioriate field"

            return (container, partitionKey, container.ReadItemAsync(op.Id, partitionKey) |> Async.AwaitTask)
        }

    match result with
    | Some(container, pk, result) ->
        [ async {
            let! currentItemResponse = result

            let newItem = op.Updater currentItemResponse.Value

            let! newItemResponse = container.ReplaceItemAsync(newItem, op.Id, Nullable pk) |> Async.AwaitTask

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

            let partitionKey =
                match PartitionKeyAttributeTools.findPartitionKey<'T>() with
                | Some name -> PartitionKey name
                | None ->
                    failwith
                        "Unable to determine partition key from type, ensure there is a [<PartitionKey>] attribute on the apprioriate field"

            return container.DeleteItemAsync(op.Id, partitionKey) |> Async.AwaitTask
        }

    match result with
    | Some result ->
        [ async {
            let! currentItemResponse = result
            return currentItemResponse.Value } ] |> AsyncSeq.ofSeqAsync

    | None -> failwith "Unable to read from the container to get the item for updating"
