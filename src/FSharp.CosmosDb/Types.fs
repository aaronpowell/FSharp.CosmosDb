namespace FSharp.CosmosDb

open Microsoft.Azure.Cosmos

type ConnectionOperation =
    { Options: CosmosClientOptions option
      FromConnectionString: bool
      Endpoint: string option
      AccessKey: string option
      ConnectionString: string option
      DatabaseId: string option
      ContainerName: string option }

// --- Operation Types --- //
type QueryOp<'T> =
    { Connection: ConnectionOperation
      Query: string option
      Parameters: (string * obj) list }

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

type DeleteOp<'T> =
    { Connection: ConnectionOperation
      Id: string
      PartitionKey: string }

type ReadOp<'T> =
    { Connection: ConnectionOperation
      Id: string
      PartitionKey: string }

type ReplaceOp<'T> =
    { Connection: ConnectionOperation
      Item: 'T }

type ContainerOperation<'T> =
    | Query of QueryOp<'T>
    | Insert of InsertOp<'T>
    | Update of UpdateOp<'T>
    | Delete of DeleteOp<'T>
    | Upsert of UpsertOp<'T>
    | Read of ReadOp<'T>
    | Replace of ReplaceOp<'T>

type ChangeFeedOptions<'T> =
    { Connection: ConnectionOperation
      Processor: string
      OnChange: Microsoft.Azure.Cosmos.Container.ChangesHandler<'T>
      InstanceName: string option
      LeaseContainer: ConnectionOperation option }
