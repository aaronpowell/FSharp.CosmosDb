namespace FSharp.CosmosDb

open Azure
open Azure.Core
open Microsoft.Azure.Cosmos
open System.Threading
open System.Threading.Tasks
open System.Collections.Concurrent
open System
open System.Collections.Generic

module internal Caching =
    let private clientCache = ConcurrentDictionary<string, CosmosClient>()

    let inline private add key builder (cd: ConcurrentDictionary<_, _>) =
        cd.AddOrUpdate(key, (fun _ -> builder()), (fun _ _ -> builder()))

    let fromConnStr connectionString clientOps =
        clientCache
        |> add connectionString (fun () -> new CosmosClient(connectionString, clientOps))

    let fromKey host accessKey clientOps =
        let connStr = $"%s{host}%s{accessKey}"
        clientCache
        |> add connStr (fun () -> new CosmosClient(host, accessKey, clientOps))
        
    let fromToken host token clientOps =
        let connStr = $"%s{host}%s{token.ToString()}"
        clientCache
        |> add connStr (fun () -> new CosmosClient(host, (token: TokenCredential), clientOps))
        
type ConnectionParameters =
    | Host of endpoint:string
    | ConnectionString of connectionString: string
    | KeyCredential of endpoint:string * credential: string
    | Token of endpoint:string * TokenCredential

type ConnectionOperation =
    { Options: CosmosClientOptions option
      ConnectionParameters: ConnectionParameters
      DatabaseId: string option
      ContainerName: string option }

    member internal this.GetClient() =
        let clientOps =
            match this.Options with
            | Some op -> op
            | None -> CosmosClientOptions()

        match this.ConnectionParameters with
        | ConnectionString connectionString -> Caching.fromConnStr connectionString clientOps
        | KeyCredential(endpoint, credential) -> Caching.fromKey endpoint credential clientOps
        | Token(endpoint, token) -> Caching.fromToken endpoint token clientOps
        | Host _ -> failwith "No credentials provided"
        
    interface IDisposable with
        override this.Dispose() =
            let client = this.GetClient()
            client.Dispose()

// --- Operation Types --- //
type QueryOp<'T> =
    { Connection: ConnectionOperation option
      Query: string option
      Parameters: (string * obj) list }

type CheckIfDatabaseExistsOp = { Connection: ConnectionOperation }

type InsertOp<'T> =
    { Connection: ConnectionOperation
      Values: 'T list }

type UpsertOp<'T> =
    { Connection: ConnectionOperation
      Values: 'T list }

type UpdateOp<'T> =
    { Connection: ConnectionOperation
      Id: string
      PartitionKey: string
      Updater: 'T -> 'T }

type DeleteItemOp<'T> =
    { Connection: ConnectionOperation
      Id: string
      PartitionKey: string }

type GetContainerPropertiesOp = { Connection: ConnectionOperation }

type CheckIfContainerExistsOp = { Connection: ConnectionOperation }

type CreateDatabaseOp = { Connection: ConnectionOperation }

type CreateDatabaseIfNotExistsOp = { Connection: ConnectionOperation }

type CreateContainerOp<'T> = { Connection: ConnectionOperation }

type CreateContainerIfNotExistsOp<'T> = { Connection: ConnectionOperation }

type DeleteContainerOp<'T> = { Connection: ConnectionOperation }

type DeleteContainerIfExistsOp = { Connection: ConnectionOperation }

type ReadOp<'T> =
    { Connection: ConnectionOperation
      Id: string
      PartitionKey: string }

type ReplaceOp<'T> =
    { Connection: ConnectionOperation
      Item: 'T }

type ChangeFeedOptions<'T> =
    { Connection: ConnectionOperation
      Processor: string
      OnChange: IReadOnlyCollection<'T> -> CancellationToken -> Task
      InstanceName: string option
      LeaseContainer: ConnectionOperation option
      PollingInterval: TimeSpan option
      StartTime: DateTime option
      MaxItems: int option }
