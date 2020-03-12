# FSharp.CosmosDb 🌍

This project is a wrapper around the [Cosmos DB](https://docs.microsoft.com/azure/cosmos-db/introduction?WT.mc_id=fsharpcosmosdb-github-aapowell) [v4 .NET SDK](https://docs.microsoft.com/azure/cosmos-db/create-sql-api-dotnet-v4?WT.mc_id=fsharpcosmosdb-github-aapowell) to make it a bit more friendly to the F# language.

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
open FSharp.CosmosDb

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

# FSharp.CosmosDb.Analyzer 💡

Also part of this repo is a [F# Analyzer](https://github.com/ionide/FSharp.Analyzers.SDK) for use from the CLI or in Ionide.

![Analyzer in action](/docs/images/cosmos-analyzer-usage.gif)

## Features

- Validation of database name against databases in Cosmos
  - Quick fix provided with list of possible db names
- Validation of container name against containers in the database
  - Quick fix provided with list of possible container names
- Detection of unused parameters in the query
  - Quick fix provided with list of defined parameters (if any)
- Detection of supplied but unused parameters
  - Quick fix provided with list of declared parameters

## Usage

### 1. Set two environment variables:

- `FSHARP_COSMOS_HOST` -> The host address of your Cosmos DB
- `FSHARP_COSMOS_KEY` -> The access key of your Cosmos DB

### 2. Install the Analyzer from paket

`paket add FSharp.Cosmos.Analyzer --group Analyzers`

### 3. Enable Analyzers in Ionide

Add the following settings (globally or in the workspace):

```json
{
  "FSharp.enableAnalyzers": true,
  "FSharp.analyzersPath": ["./packages/analyzers"]
}
```

# License

[MIT](./License.md)

# Thank Yous

- Zaid Ajaj for the [Npgsql Analyzer](https://github.com/Zaid-Ajaj/Npgsql.FSharp.Analyzer). Without this I wouldn't have been able to work out how to do it (and there's some code lifted from there)
- [Krzysztof Cieślak](https://twitter.com/k_cieslak) for the amazing Ionide plugin
