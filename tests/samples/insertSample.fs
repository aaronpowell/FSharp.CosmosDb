module InsertSamples

open FSharp.CosmosDb

let key = "testing"
let endpointUrl = "https://localhost"

[<CLIMutable>]
type User =
    { Id: string
      Name: string }

let insertUser() =
    endpointUrl
    |> Cosmos.host
    |> Cosmos.connect key
    |> Cosmos.database "UserDb"
    |> Cosmos.container "UserContainer"
    |> Cosmos.insert
        { Id = ""
          Name = "" }
    |> Cosmos.execAsync
