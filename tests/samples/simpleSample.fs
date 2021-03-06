module FirstSample

open FSharp.CosmosDb

let key = "testing"
let endpointUrl = "https://localhost"

let findUsers() =
    endpointUrl
    |> Cosmos.host
    |> Cosmos.connect key
    |> Cosmos.database "UserDb"
    |> Cosmos.container "UserContainer"
