module samples.createDeleteContainerSample

open FSharp.CosmosDb

let connectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=..."

let createDatabase () =
    connectionString
    |> Cosmos.fromConnectionString
    |> Cosmos.database "MyDatabase"
    |> Cosmos.container "MyContainer"
    |> Cosmos.createContainer
    |> Cosmos.execAsync
    |> Async.RunSynchronously

let deleteDatabase () =
    connectionString
    |> Cosmos.fromConnectionString
    |> Cosmos.database "MyDatabase"
    |> Cosmos.container "MyContainer"
    |> Cosmos.deleteContainer
    |> Cosmos.execAsync
    |> Async.RunSynchronously
