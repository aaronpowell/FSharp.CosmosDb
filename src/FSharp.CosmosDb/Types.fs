namespace FSharp.CosmosDb

open FSharp.CosmosDb
open Microsoft.Azure.Cosmos
open System.Threading
open System.Threading.Tasks
open System.Collections.Concurrent
open System
open System.Collections.Generic

module internal Caching =
    let private clientCache =
        ConcurrentDictionary<string, CosmosClient>()

    let inline private tryGetOption key (cd: ConcurrentDictionary<_, _>) =
        if cd.ContainsKey key then
            Some(cd.[key])
        else
            None

    let fromConnStr connStr clientOps =
        maybe {
            let! cs = connStr

            return
                clientCache
                |> tryGetOption cs
                |> Option.defaultWith
                    (fun () ->
                        let client = new CosmosClient(cs, clientOps)
                        clientCache.[cs] <- client
                        client)
        }

    let fromKey host' accessKey' clientOps =
        maybe {
            let! host = host'
            let! accessKey = accessKey'

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

type ConnectionOperation =
    { Options: CosmosClientOptions option
      FromConnectionString: bool
      Endpoint: string option
      AccessKey: string option
      ConnectionString: string option
      DatabaseId: string option
      ContainerName: string option }

    member internal this.GetClient() =
        let clientOps =
            match this.Options with
            | Some op -> op
            | None -> CosmosClientOptions()

        let client =
            if this.FromConnectionString then
                Caching.fromConnStr this.ConnectionString clientOps
            else
                Caching.fromKey this.Endpoint this.AccessKey clientOps

        match client with
        | Some client -> client
        | None -> failwith "No connection information provided"

    interface IDisposable with
        override this.Dispose() =
            let client = this.GetClient()
            client.Dispose()

// --- Operation Types --- //
type QueryOp<'T> =
    { Connection: ConnectionOperation
      Query: string option
      Parameters: (string * obj) list }

type CheckIfDatabaseExistsOp =
    { Connection: ConnectionOperation }

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
    
type GetContainerPropertiesOp = 
    { Connection: ConnectionOperation }
    
type CheckIfContainerExistsOp =
    { Connection: ConnectionOperation }
    
type DeleteContainerOp<'T> =
    { Connection: ConnectionOperation }

type DeleteContainerIfExistsOp =
    { Connection: ConnectionOperation }
    
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
