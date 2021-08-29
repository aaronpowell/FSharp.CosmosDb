namespace FSharp.CosmosDb

open Microsoft.Azure.Cosmos
open System.Collections.Concurrent

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

    let query<'T> query op : ContainerOperation<'T> =
        Query
            { defaultQueryOp () with
                  Query = Some query
                  Connection = op }

    let parameters arr op =
        match op with
        | Query q ->
            Query
                { q with
                      Parameters = q.Parameters @ arr }
        | _ -> failwith "Only the Query discriminated union supports parameters"

    // --- INSERT --- //

    let insertMany<'T> (values: 'T list) op =
        Insert { Connection = op; Values = values }

    let insert<'T> (value: 'T) op =
        Insert { Connection = op; Values = [ value ] }

    // --- INSERT --- //

    let upsertMany<'T> (values: 'T list) op =
        Upsert { Connection = op; Values = values }

    let upsert<'T> (value: 'T) op =
        Upsert { Connection = op; Values = [ value ] }

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


    // --- READ --- //

    let read id partitionKey op =
        Read
            { Connection = op
              Id = id
              PartitionKey = partitionKey }

    // --- REPLACE --- //

    let replace<'T> (item: 'T) op =
        Replace { Connection = op; Item = item }

    // --- Execute --- //

    let private clientCache =
        ConcurrentDictionary<string, CosmosClient>()

    let inline private tryGetOption key (cd: ConcurrentDictionary<_, _>) =
        if cd.ContainsKey key then
            Some(cd.[key])
        else
            None

    let inline private getFromConnStr connInfo clientOps =
        maybe {
            let! connStr = connInfo.ConnectionString

            return
                clientCache
                |> tryGetOption connStr
                |> Option.defaultWith
                    (fun () ->
                        let client = new CosmosClient(connStr, clientOps)
                        clientCache.[connStr] <- client
                        client)
        }

    let inline private getFromHost connInfo clientOps =
        maybe {
            let! host = connInfo.Endpoint
            let! accessKey = connInfo.AccessKey

            let connStr = sprintf "%s%s" host accessKey

            return
                clientCache
                |> tryGetOption connStr
                |> Option.defaultWith
                    (fun () ->
                        let client =
                            new CosmosClient(host, accessKey, clientOps)

                        clientCache.[connStr] <- client
                        client)
        }

    let private getClient connInfo =
        let clientOps =
            match connInfo.Options with
            | Some op -> op
            | None -> CosmosClientOptions()

        let client =
            if connInfo.FromConnectionString then
                getFromConnStr connInfo clientOps
            else
                getFromHost connInfo clientOps

        match client with
        | Some client -> client
        | None -> failwith "No connection information provided"

    let dispose connInfo =
        let client = getClient connInfo
        client.Dispose()

    let execAsync<'T> (op: ContainerOperation<'T>) =
        match op with
        | Query op -> OperationHandling.execQuery getClient op
        | Insert op -> OperationHandling.execInsert getClient op
        | Update op -> OperationHandling.execUpdate getClient op
        | Delete op -> OperationHandling.execDelete getClient op
        | Upsert op -> OperationHandling.execUpsert getClient op
        | Read op -> OperationHandling.execRead getClient op
        | Replace op -> OperationHandling.execReplace getClient op

    let execBatchAsync<'T> batchSize (op: ContainerOperation<'T>) =
        match op with
        | Query op ->
            let queryOps = QueryRequestOptions()
            queryOps.MaxItemCount <- batchSize
            OperationHandling.execQueryBatch getClient op queryOps
        | _ -> failwith "Batch return operation only supported with query operations, use `execAsync` instead."

    // --- Access Cosmos APIs directly --- //

    [<RequireQualifiedAccess>]
    module Raw =
        let client connInfo = getClient connInfo

        let database connInfo =
            maybe {
                let client = getClient connInfo
                let! dbName = connInfo.DatabaseId

                return client.GetDatabase dbName
            }

        let container connInfo =
            maybe {
                let client = getClient connInfo
                let! dbName = connInfo.DatabaseId

                let db = client.GetDatabase dbName

                let! cn = connInfo.ContainerName

                return db.GetContainer cn
            }

    [<RequireQualifiedAccess>]
    module ChangeFeed =
        let create<'T> processor onChange connInfo : ChangeFeedOptions<'T> =
            { Processor = processor
              OnChange = onChange
              Connection = connInfo
              LeaseContainer = None
              InstanceName = None
              PollingInterval = None
              StartTime = None
              MaxItems = None }

        let withInstanceName<'T> name changeFeedInfo : ChangeFeedOptions<'T> =
            { changeFeedInfo with
                  InstanceName = Some name }

        let leaseContainer<'T> leaseContainerInfo changeFeedInfo : ChangeFeedOptions<'T> =
            { changeFeedInfo with
                  LeaseContainer = Some leaseContainerInfo }

        let pollingInterval<'T> interval changeFeedInfo : ChangeFeedOptions<'T> =
            { changeFeedInfo with
                  PollingInterval = Some interval }

        let startTime<'T> startTime changeFeedInfo : ChangeFeedOptions<'T> =
            { changeFeedInfo with
                  StartTime = Some startTime }

        let maxItems<'T> maxItems changeFeedInfo : ChangeFeedOptions<'T> =
            { changeFeedInfo with
                  MaxItems = Some maxItems }

        let build<'T> changeFeedInfo =
            let processor =
                maybe {
                    let! container = Raw.container changeFeedInfo.Connection

                    return
                        container.GetChangeFeedProcessorBuilder<'T>(
                            changeFeedInfo.Processor,
                            (fun changes cancellationToken -> changeFeedInfo.OnChange changes cancellationToken)
                        )
                }

            match processor with
            | Some processor ->
                processor
                |> fun c ->
                    match changeFeedInfo.InstanceName with
                    | Some i -> c.WithInstanceName i
                    | None -> c
                |> fun c ->
                    match changeFeedInfo.PollingInterval with
                    | Some i -> c.WithPollInterval i
                    | None -> c
                |> fun c ->
                    match changeFeedInfo.StartTime with
                    | Some x -> c.WithStartTime x
                    | None -> c
                |> fun c ->
                    match changeFeedInfo.MaxItems with
                    | Some x -> c.WithMaxItems x
                    | None -> c
                |> ignore

                maybe {
                    let! leaseContainer = changeFeedInfo.LeaseContainer
                    let! container = Raw.container leaseContainer

                    return processor.WithLeaseContainer container
                }
                |> ignore

                processor.Build()
            | None ->
                failwith "Unable to connect the change feed. Ensure the container and lease container info is all set"
