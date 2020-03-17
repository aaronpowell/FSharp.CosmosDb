namespace FSharp.CosmosDb

open Azure.Cosmos
open FSharp.Control

type ConnectionOperation =
    { Options: CosmosClientOptions option
      FromConnectionString: bool
      Endpoint: string option
      AccessKey: string option
      ConnectionString: string option
      DatabaseId: string option
      ContainerName: string option }

type QueryOperation =
    { Connection: ConnectionOperation
      Query: string option
      Parameters: (string * obj) list }

module Cosmos =
    let private defaultConnectionOp =
        { Options = None
          FromConnectionString = false
          Endpoint = None
          AccessKey = None
          ConnectionString = None
          DatabaseId = None
          ContainerName = None }

    let private defaultQueryOp =
        { Connection = defaultConnectionOp
          Query = None
          Parameters = [] }

    let fromConnectionString connString =
        { defaultConnectionOp with
              FromConnectionString = true
              ConnectionString = connString }

    let fromConnectionStringWithOptions connString op =
        { defaultConnectionOp with
              Options = Some op
              FromConnectionString = true
              ConnectionString = connString }

    let host endpoint = { defaultConnectionOp with Endpoint = Some endpoint }

    let connectWithOptions options accessKey op =
        { op with
              Options = Some options
              AccessKey = accessKey }

    let connect accessKey op = { op with AccessKey = Some accessKey }

    let database dbId op = { op with DatabaseId = Some dbId }

    let container cn op = { op with ContainerName = Some cn }

    let query query op =
        { defaultQueryOp with
              Query = Some query
              Connection = op }

    let parameters arr op = { op with Parameters = arr }

    let execAsync<'T> op =
        let connInfo = op.Connection
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
        | None -> failwith "No connection information provided"
        | Some client ->
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
