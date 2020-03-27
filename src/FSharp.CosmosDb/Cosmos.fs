namespace FSharp.CosmosDb

open Azure.Cosmos
open FSharp.Control
open System
open System.Reflection

[<RequireQualifiedAccess>]
module Cosmos =
    type ConnectionOperation =
        private { Options: CosmosClientOptions option
                  FromConnectionString: bool
                  Endpoint: string option
                  AccessKey: string option
                  ConnectionString: string option
                  DatabaseId: string option
                  ContainerName: string option }

    let private defaultConnectionOp() =
        { Options = None
          FromConnectionString = false
          Endpoint = None
          AccessKey = None
          ConnectionString = None
          DatabaseId = None
          ContainerName = None }

    let fromConnectionString connString =
        { defaultConnectionOp() with
              FromConnectionString = true
              ConnectionString = Some connString }

    let fromConnectionStringWithOptions connString op =
        { defaultConnectionOp() with
              Options = Some op
              FromConnectionString = true
              ConnectionString = Some connString }

    let host endpoint = { defaultConnectionOp() with Endpoint = Some endpoint }

    let connectWithOptions options accessKey op =
        { op with
              Options = Some options
              AccessKey = accessKey }

    let connect accessKey op = { op with AccessKey = Some accessKey }

    let database dbId op = { op with DatabaseId = Some dbId }

    let container cn op = { op with ContainerName = Some cn }

    // --- Operation Types --- //
    type QueryOp =
        private { Connection: ConnectionOperation
                  Query: string option
                  Parameters: (string * obj) list }

    type InsertOp<'T> =
        private { Connection: ConnectionOperation
                  Values: 'T list }

    type ContainerOperation<'T> =
        | Query of QueryOp
        | Insert of InsertOp<'T>

    // --- QUERY --- //

    let private defaultQueryOp() =
        { Connection = defaultConnectionOp()
          Query = None
          Parameters = [] }

    let query query op =
        Query
            { defaultQueryOp() with
                  Query = Some query
                  Connection = op }

    let parameters arr op =
        match op with
        | Query q -> Query { q with Parameters = arr }
        | _ -> failwith "Only the Query discriminated union supports parameters"

    // --- INSERT --- //

    let insertMany<'T> (values: 'T list) op =
        Insert
            { Connection = op
              Values = values }

    let insert<'T> (value: 'T) op =
        Insert
            { Connection = op
              Values = [ value ] }

    // --- Execute --- //

    let private getClient connInfo =
        let clientOps =
            match connInfo.Options with
            | Some op -> op
            | None -> CosmosClientOptions()

        let client =
            if connInfo.FromConnectionString then
                maybe {
                    let! connStr = connInfo.ConnectionString
                    return new CosmosClient(connStr, clientOps) }
            else
                maybe {
                    let! host = connInfo.Endpoint
                    let! accessKey = connInfo.AccessKey
                    return new CosmosClient(host, accessKey, clientOps) }

        match client with
        | Some client -> client
        | None -> failwith "No connection information provided"


    let execAsync<'T> (op: ContainerOperation<'T>) =
        match op with
        | Query op ->
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
                failwith
                    "Unable to construct a query as some values are missing across the database, container name and query"
        | Insert op ->
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
                               |> List.map (fun single ->
                                   container.CreateItemAsync<'T>(single, partitionKey) |> Async.AwaitTask)
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
                failwith
                    "Unable to construct a query as some values are missing across the database, container name and query"
