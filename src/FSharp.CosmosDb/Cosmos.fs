namespace FSharp.CosmosDb

open Microsoft.Azure.Cosmos
open System

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
        { defaultConnectionOp () with Endpoint = Some endpoint }

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

    let query<'T> query op : QueryOp<'T> =
        { defaultQueryOp () with
            Query = Some query
            Connection = op }

    let parameters arr op =
        { op with QueryOp.Parameters = op.Parameters @ arr }

    // --- DATABASE EXISTS --- //
    let databaseExists<'T> op =
        { CheckIfDatabaseExistsOp.Connection = op }

    // --- CREATE DATABASE --- //
    let createDatabase op = { CreateDatabaseOp.Connection = op }

    let createDatabaseIfNotExists op =
        { CreateDatabaseIfNotExistsOp.Connection = op }

    // --- INSERT --- //

    let insertMany<'T> (values: 'T list) op =
        { InsertOp.Connection = op
          Values = values }

    let insert<'T> (value: 'T) op =
        { InsertOp.Connection = op
          Values = [ value ] }

    // --- INSERT --- //

    let upsertMany<'T> (values: 'T list) op =
        { UpsertOp.Connection = op
          Values = values }

    let upsert<'T> (value: 'T) op =
        { UpsertOp.Connection = op
          Values = [ value ] }

    // --- UPDATE --- //

    let update<'T> id partitionKey (updater: 'T -> 'T) op =
        { UpdateOp.Connection = op
          Id = id
          PartitionKey = partitionKey
          Updater = updater }

    // --- DELETE ITEM --- //

    let deleteItem<'T> id partitionKey op =
        { DeleteItemOp.Connection = op
          Id = id
          PartitionKey = partitionKey }

    // --- GET CONTAINER PROPERTIES --- //
    let getContainerProperties op =
        { GetContainerPropertiesOp.Connection = op }

    // --- CONTAINER EXISTS --- //
    let containerExists op =
        { CheckIfContainerExistsOp.Connection = op }

    // --- CREATE CONTAINER --- //
    let createContainer<'T> op : CreateContainerOp<'T> = { CreateContainerOp.Connection = op }

    let createContainerIfNotExists<'T> op : CreateContainerIfNotExistsOp<'T> =
        { CreateContainerIfNotExistsOp.Connection = op }

    // --- DELETE CONTAINER --- //

    let deleteContainer<'T> op : DeleteContainerOp<'T> = { DeleteContainerOp.Connection = op }

    // --- DELETE CONTAINER IF EXISTS --- //

    let deleteContainerIfExists op : DeleteContainerIfExistsOp =
        { DeleteContainerIfExistsOp.Connection = op }

    // --- READ --- //

    let read id partitionKey op =
        { ReadOp.Connection = op
          Id = id
          PartitionKey = partitionKey }

    // --- REPLACE --- //

    let replace<'T> (item: 'T) op =
        { ReplaceOp.Connection = op
          Item = item }

    // --- Execute --- //

    let private getClient (connInfo: ConnectionOperation) = connInfo.GetClient()

    let dispose (connInfo: ConnectionOperation) = (connInfo :> IDisposable).Dispose()

    let execBatchAsync<'T> batchSize op =
        let queryOps = QueryRequestOptions()
        queryOps.MaxItemCount <- batchSize
        OperationHandling.execQueryBatch getClient op queryOps

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
            { changeFeedInfo with InstanceName = Some name }

        let leaseContainer<'T> leaseContainerInfo changeFeedInfo : ChangeFeedOptions<'T> =
            { changeFeedInfo with LeaseContainer = Some leaseContainerInfo }

        let pollingInterval<'T> interval changeFeedInfo : ChangeFeedOptions<'T> =
            { changeFeedInfo with PollingInterval = Some interval }

        let startTime<'T> startTime changeFeedInfo : ChangeFeedOptions<'T> =
            { changeFeedInfo with StartTime = Some startTime }

        let maxItems<'T> maxItems changeFeedInfo : ChangeFeedOptions<'T> =
            { changeFeedInfo with MaxItems = Some maxItems }

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

type Cosmos =
    static member private getClient(connInfo: ConnectionOperation) = connInfo.GetClient()

    static member execAsync(op: QueryOp<'T>) =
        OperationHandling.execQuery Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execCheckIfDatabaseExists Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execInsert Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execUpdate Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execDeleteItem Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execGetContainerProperties Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execCheckIfContainerExists Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execDeleteContainer Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execDeleteContainerIfExists Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execUpsert Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execRead Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execReplace Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execCreateContainer Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execCreateContainerIfNotExists Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execCreateDatabase Cosmos.getClient op

    static member execAsync op =
        OperationHandling.execCreateDatabaseIfNotExists Cosmos.getClient op
