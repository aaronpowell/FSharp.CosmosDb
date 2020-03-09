namespace FSharp.CosmosDb

open Azure.Cosmos
open FSharp.Control

type CosmosOperation =
    { Options: CosmosClientOptions option
      Endpoint: string
      AccessKey: string option
      DatabaseId: string option
      ContainerName: string option
      Query: string option
      Parameters: (string * obj) list }

module Cosmos =
    let host endpoint =
        { Options = None
          Endpoint = endpoint
          AccessKey = None
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

        match op.AccessKey with
        | Some connectionString ->
            match op.DatabaseId with
            | Some dbId ->
                match op.ContainerName with
                | Some cn ->
                    match op.Query with
                    | Some query ->
                        let client = new CosmosClient(op.Endpoint, connectionString, clientOps)
                        let db = client.GetDatabase dbId
                        let container = db.GetContainer cn
                        let qd = QueryDefinition query
                        op.Parameters
                        |> List.map (fun (key, value) -> qd.WithParameter(key, value))
                        |> ignore

                        container.GetItemQueryIterator<'T> qd |> AsyncSeq.ofAsyncEnum
                    | None -> failwith "No query provided"
                | None -> failwith "No container name provided"
            | None -> failwith "No dabase id provided"
        | None -> failwith "No access key provided"
