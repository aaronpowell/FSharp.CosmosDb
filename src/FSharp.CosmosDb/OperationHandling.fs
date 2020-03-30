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
