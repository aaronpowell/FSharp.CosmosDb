namespace FSharp.CosmosDb

open Azure.Cosmos

type ConnectionOperation =
    { Options: CosmosClientOptions option
      FromConnectionString: bool
      Endpoint: string option
      AccessKey: string option
      ConnectionString: string option
      DatabaseId: string option
      ContainerName: string option }

// --- Operation Types --- //
type QueryOp =
    { Connection: ConnectionOperation
      Query: string option
      Parameters: (string * obj) list }

type InsertOp<'T> =
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

type ContainerOperation<'T> =
    | Query of QueryOp
    | Insert of InsertOp<'T>
    | Update of UpdateOp<'T>
    | Delete of DeleteOp<'T>
