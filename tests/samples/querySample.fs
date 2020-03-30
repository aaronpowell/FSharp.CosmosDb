module QuerySample

open FSharp.CosmosDb

let key = "testing"
let endpointUrl = "https://localhost"

[<CLIMutable>]
type User =
    { Id: string
      Name: string }

let findUsers() =
    endpointUrl
    |> Cosmos.host
    |> Cosmos.connect key
    |> Cosmos.database "UserDb"
    |> Cosmos.container "UserContainer"
    |> Cosmos.query "SELECT * FROM u WHERE u.Name = @name"
    |> Cosmos.parameters [ "name", box "Aaron" ]
    |> Cosmos.execAsync<User>
