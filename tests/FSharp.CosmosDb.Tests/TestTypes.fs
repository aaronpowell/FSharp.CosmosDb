[<AutoOpen>]
module TestTypes

open FSharp.CosmosDb

type TestType =
    { [<Id>]
      Id: string
      [<PartitionKey>]
      PK: string }
