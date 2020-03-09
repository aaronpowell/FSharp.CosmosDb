# FSharp.CosmosDb 🌍

This project is a wrapper around the [Cosmos DB v4 .NET SDK](https://docs.microsoft.com/en-us/azure/cosmos-db/create-sql-api-dotnet-v4?fsharpcosmosdb-github-aapowell) to make it a bit more friendly to the F# language.

## Install

Install via NuGet:

```
dotnet add package FSharp.CosmosDb
```

Or using Paket:

```
dotnet paket add FSharp.CosmosDb
```

## Usage

### Query

```f#
open FSharp.Cosmos

let host = "https://..."
let key = "..."
let findUsers() =
    host
    |> Cosmos.host
    |> Cosmos.connect key
    |> Cosmos.database "UserDb"
    |> Cosmos.container |> "UserContainer"
    |> Cosmos.query "SELECT u.FirstName, u.LastName FROM u WHERE u.LastName = @name"
    |> Cosmos.parameters [ "name", box "Powell" ]
    |> Cosmos.execAsync<User>
```

The result from a query is an `AsyncSeq` via [FSharp.Control.AsyncSeq](http://fsprojects.github.io/FSharp.Control.AsyncSeq/index.html).

```f#
[<EntryPoint>]
let main argv =
    async {
        let users = findUsers()
        do! users
        |> AsyncSeq.iter (fun u -> printfn "%s %s" u.FirstName u.LastName)

        return 0
    } |> Async.RunSynchronously
```
