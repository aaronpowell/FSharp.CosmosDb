namespace FSharp.CosmosDb

open Azure.Cosmos
open FSharp.Control
open System
open System.Reflection

[<RequireQualifiedAccess>]
module Cosmos =
    let private defaultConnectionOp () =
        { Options = None
          FromConnectionString = false
          Endpoint = None
          AccessKey = None
          ConnectionString = None
          DatabaseId = None
          ContainerName = None }

    let fromConnectionString connString =
        { defaultConnectionOp () with
              FromConnectionString = true
              ConnectionString = Some connString }

    let fromConnectionStringWithOptions connString op =
        { defaultConnectionOp () with
              Options = Some op
              FromConnectionString = true
              ConnectionString = Some connString }

    let host endpoint =
        { defaultConnectionOp () with
              Endpoint = Some endpoint }

    let connectWithOptions options accessKey op =
        { op with
              Options = Some options
              AccessKey = accessKey }

    let connect accessKey op = { op with AccessKey = Some accessKey }

    let database dbId op = { op with DatabaseId = Some dbId }

    let container cn op = { op with ContainerName = Some cn }

    // --- QUERY --- //

    let private defaultQueryOp () =
        { Connection = defaultConnectionOp ()
          Query = None
          Parameters = [] }

    let query query op =
        Query
            { defaultQueryOp () with
                  Query = Some query
                  Connection = op }

    let parameters arr op =
        match op with
        | Query q -> Query { q with Parameters = arr }
        | _ -> failwith "Only the Query discriminated union supports parameters"

    // --- INSERT --- //

    let insertMany<'T> (values: 'T list) op =
        Insert { Connection = op; Values = values }

    let insert<'T> (value: 'T) op =
        Insert { Connection = op; Values = [ value ] }

    // --- UPDATE --- //

    let update<'T> id partitionKey (updater: 'T -> 'T) op =
        Update
            { Connection = op
              Id = id
              PartitionKey = partitionKey
              Updater = updater }

    // --- DELETE --- //

    let delete<'T> id partitionKey op =
        Delete
            { Connection = op
              Id = id
              PartitionKey = partitionKey }

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
                    return new CosmosClient(connStr, clientOps)
                }
            else
                maybe {
                    let! host = connInfo.Endpoint
                    let! accessKey = connInfo.AccessKey
                    return new CosmosClient(host, accessKey, clientOps)
                }

        match client with
        | Some client -> client
        | None -> failwith "No connection information provided"

    let execAsync<'T> (op: ContainerOperation<'T>) =
        match op with
        | Query op -> OperationHandling.execQuery getClient op
        | Insert op -> OperationHandling.execInsert getClient op
        | Update op -> OperationHandling.execUpdate getClient op
        | Delete op -> OperationHandling.execDelete getClient op

    let execBatchAsync<'T> (op: ContainerOperation<'T>) =
        match op with
        | Query op -> OperationHandling.execQueryBatch getClient op
        | _ -> failwith "Batch return operation only supported with query operations, use `execAsync` instead."
