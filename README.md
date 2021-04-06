# FSharp.CosmosDb 🌍

![Latest Build](https://github.com/aaronpowell/FSharp.CosmosDb/workflows/Build%20release%20candidate/badge.svg) ![Latest Release](https://github.com/aaronpowell/FSharp.CosmosDb/workflows/Publish%20Release/badge.svg) [![NuGet Badge - FSharp.CosmosDb](https://buildstats.info/nuget/FSharp.CosmosDb)](https://www.nuget.org/packages/FSharp.CosmosDb) [![The MIT License](https://img.shields.io/badge/license-MIT-orange.svg?color=blue&style=flat-square)](http://opensource.org/licenses/MIT)

This project is a wrapper around the [Cosmos DB](https://docs.microsoft.com/azure/cosmos-db/introduction?WT.mc_id=javascript-0000-aapowell) [v4 .NET SDK](https://docs.microsoft.com/azure/cosmos-db/create-sql-api-dotnet-v4?WT.mc_id=javascript-0000-aapowell) to make it a bit more friendly to the F# language.

## Install

Install via NuGet:

```bash
dotnet add package FSharp.CosmosDb
```

Or using Paket:

```bash
dotnet paket add FSharp.CosmosDb
```

## Usage

All operations will return an `AsyncSeq` via [FSharp.Control.AsyncSeq](http://fsprojects.github.io/FSharp.Control.AsyncSeq/index.html) that contains the data fetched, data inserted or data updated.

### Insert

```fsharp
open FSharp.CosmosDb

let connStr = "..."

let insertUsers data =
    connStr
    |> Cosmos.fromConnectionString
    |> Cosmos.database "UserDb"
    |> Cosmos.container "UserContainer"
    |> Cosmos.insertMany<User> data
    |> Cosmos.execAsync
```

### Upsert

```fsharp
open FSharp.CosmosDb

let connStr = "..."

let insertUsers data =
    connStr
    |> Cosmos.fromConnectionString
    |> Cosmos.database "UserDb"
    |> Cosmos.container "UserContainer"
    |> Cosmos.upsertMany<User> data
    |> Cosmos.execAsync
```

### Update

```fsharp
open FSharp.CosmosDb

let connStr = "..."

let updateUser id partitionKey =
    connStr
    |> Cosmos.fromConnectionString
    |> Cosmos.database "UserDb"
    |> Cosmos.container "UserContainer"
    |> Cosmos.update<User> id partitionKey (fun user -> { user with IsRegistered = true })
    |> Cosmos.execAsync
```

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
    |> Cosmos.container "UserContainer"
    |> Cosmos.query "SELECT u.FirstName, u.LastName FROM u WHERE u.LastName = @name"
    |> Cosmos.parameters [ "@name", box "Powell" ]
    |> Cosmos.execAsync<User>
```

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

### Delete

```fsharp
open FSharp.CosmosDb

let connStr = "..."

let updateUser id partitionKey =
    connStr
    |> Cosmos.fromConnectionString
    |> Cosmos.database "UserDb"
    |> Cosmos.container "UserContainer"
    |> Cosmos.deleteItem id partitionKey
    |> Cosmos.execAsync
```

# FSharp.CosmosDb.Analyzer 💡

[![NuGet Badge - FSharp.CosmosDb](https://buildstats.info/nuget/FSharp.CosmosDb)](https://www.nuget.org/packages/FSharp.CosmosDb)

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

## Analyzer Usage

### 1. Provide connection information

Connection information can be provided as either environment variables or using an `appsettings.json`/`appsettings.Development.json` file.

#### Environment Variables

The analyzer will look for the following environment variables:

- `FSHARP_COSMOS_CONNSTR` -> A full connection string to Cosmos DB
- `FSHARP_COSMOS_HOST` & `FSHARP_COSMOS_KEY` -> The URI endpoint and access key

The `FSHARP_COSMOS_CONNSTR` will take precedence if both sets of environment variables are provided

#### App Settings

The analyzer will look for a file matching `appsettings.json` or `appsettings.Development.json` in either the workspace root of the VS Code instance or relative to the file being parsed. The file is expected to have the following JSON structure in it:

```json
{
  "CosmosConnection": {
    "ConnectionString": "",
    "Host": "",
    "Key": ""
  }
}
```

If `CosmosConnection.ConnectionString` exists, it will be used, otherwise it will use the `CosmosConnection.Host` and `CosmosConnection.Key` to connect.

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

# Thank You

- Zaid Ajaj for the [Npgsql Analyzer](https://github.com/Zaid-Ajaj/Npgsql.FSharp.Analyzer). Without this I wouldn't have been able to work out how to do it (and there's some code lifted from there)
- [Krzysztof Cieślak](https://twitter.com/k_cieslak) for the amazing Ionide plugin
- [Isaac Abraham](https://twitter.com/isaac_abraham) for helping fix the parser
