namespace FSharp.CosmosDb

open Azure.Cosmos
open FSharp.Control

type CosmosOperation =
    { Options: CosmosClientOptions option
      FromConnectionString: bool
      Endpoint: string option
      AccessKey: string option
      ConnectionString: string option
      DatabaseId: string option
      ContainerName: string option
      Query: string option
      Parameters: (string * obj) list }

module Cosmos =
    let fromConnectionString connString =
        { Options = None
          FromConnectionString = true
          Endpoint = None
          AccessKey = None
          ConnectionString = connString
          DatabaseId = None
          ContainerName = None
          Query = None
          Parameters = List.empty }

    let fromConnectionStringWithOptions connString op =
        { Options = Some op
          FromConnectionString = true
          Endpoint = None
          AccessKey = None
          ConnectionString = connString
          DatabaseId = None
          ContainerName = None
          Query = None
          Parameters = List.empty }

    let host endpoint =
        { Options = None
          Endpoint = Some endpoint
          FromConnectionString = false
          AccessKey = None
          ConnectionString = None
          DatabaseId = None
          ContainerName = None
          Query = None
          Parameters = List.empty }

    let connectWithOptions options accessKey op =
        { op with
              Options = Some options
              AccessKey = accessKey }

    let connect accessKey op = { op with AccessKey = Some accessKey }

    let database dbId op = { op with DatabaseId = Some dbId }

    let container cn op = { op with ContainerName = Some cn }

    let query query op = { op with Query = Some query }

    let parameters arr op = { op with Parameters = arr }

    let execAsync<'T> op =
        let clientOps =
            match op.Options with
            | Some op -> op
            | None -> CosmosClientOptions()

        let client =
            if op.FromConnectionString then
                maybe {
                    let! connStr = op.ConnectionString
                    return new CosmosClient(connStr, clientOps) }
            else
                maybe {
                    let! host = op.Endpoint
                    let! accessKey = op.AccessKey
                    return new CosmosClient(host, accessKey, clientOps) }

        match client with
        | None -> failwith "No connection information provided"
        | Some client ->
            let result =
                maybe {
                    let! databaseId = op.DatabaseId
                    let! containerName = op.ContainerName

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
