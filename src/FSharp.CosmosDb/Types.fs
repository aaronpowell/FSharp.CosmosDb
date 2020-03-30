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

type ContainerOperation<'T> =
    | Query of QueryOp
    | Insert of InsertOp<'T>
