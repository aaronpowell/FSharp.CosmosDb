﻿module FSharp.CosmosDb.Samples

open FSharp.CosmosDb
open FSharp.Control
open Microsoft.Extensions.Configuration
open System.IO
open System.Net
open Types
open System.Net.Http
open Microsoft.Azure.Cosmos

let getFamiliesConnection host key =
    host
    |> Cosmos.host
    |> Cosmos.connect key
    |> Cosmos.database "FamilyDatabase"
    |> Cosmos.container "FamilyContainer"

let getFamiliesConnectionFromConnString connectionString =
    connectionString
    |> Cosmos.fromConnectionString
    |> Cosmos.database "FamiliesDatabase"
    |> Cosmos.container "FamiliesContainer"

let insertFamilies<'T> conn (families: 'T list) =
    conn
    |> Cosmos.insertMany<'T> families
    |> Cosmos.execAsync<'T>

// this is to test a broken query in the analyzer
let getFamiliesBroken conn =
    conn
    |> Cosmos.query<Family> "SELECT * FROM f WHERE f.Name = @name AND f.Age = @AGE"
    |> Cosmos.parameters [ "age", box 35
                           "lastName", box "Powell" ]
    |> Cosmos.execAsync

let getFamilies conn =
    conn
    |> Cosmos.query<Family> "SELECT * FROM f WHERE f.LastName = @lastName"
    |> Cosmos.parameters [ "@lastName", box "Powell" ]
    |> Cosmos.execAsync

let updateFamily conn id pk =
    conn
    |> Cosmos.update<Family> id pk (fun family -> { family with IsRegistered = not family.IsRegistered })
    |> Cosmos.execAsync

let deleteFamily conn id pk =
    conn
    |> Cosmos.deleteItem<Family> id pk
    |> Cosmos.execAsync

[<EntryPoint>]
let main argv =
    let environmentName =
        System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")

    let builder =
        JsonConfigurationExtensions
            .AddJsonFile(
                JsonConfigurationExtensions.AddJsonFile(
                    FileConfigurationExtensions.SetBasePath(ConfigurationBuilder(), Directory.GetCurrentDirectory()),
                    "appsettings.json",
                    true,
                    true
                ),
                $"appsettings.%s{environmentName}.json",
                true,
                true
            )
            .AddEnvironmentVariables()

    let config = builder.Build()

    async {
        let host = config.["Cosmos:EndPoint"]
        let key = config.["Cosmos:Key"]
        use conn = getFamiliesConnection host key

        // let connectionString =
        //     config.["Cosmos:ConnectionString"]

        // let conn =
        //     getFamiliesConnectionFromConnString connectionString

        printfn "Getting ready to do some Cosmos operations"

        do!
            conn
            |> Cosmos.createDatabaseIfNotExists
            |> Cosmos.execAsync
            |> AsyncSeq.iter (fun _ -> ())

        do!
            conn
            |> Cosmos.createContainerIfNotExists<Family>
            |> Cosmos.execAsync
            |> AsyncSeq.iter (fun _ -> ())


        let families =
            [| { Id = "Powell.1"
                 LastName = "Powell"
                 Parents =
                   [| { FamilyName = "Powell"
                        FirstName = "Aaron" } |]
                 Children = Array.empty
                 Address =
                   { State = "NSW"
                     Country = "Australia"
                     City = "Sydney" }
                 IsRegistered = true } |]
            |> Array.toList

        let insert = insertFamilies conn families

        do!
            insert
            |> AsyncSeq.iter (fun f -> printfn $"Inserted: %A{f}")

        let families = getFamilies conn

        do!
            families
            |> AsyncSeq.iter (fun f -> printfn $"Got: %A{f}")

        let updatePowell = updateFamily conn "Powell.1" "Powell"

        do!
            updatePowell
            |> AsyncSeq.iter (fun f -> printfn $"Updated: %A{f}")

        let deletePowell = deleteFamily conn "Powell.1" "Powell"

        do!
            deletePowell
            |> AsyncSeq.iter (fun f -> printfn $"Deleted: %A{f}")

        do!
            conn
            |> Cosmos.query<Family> "SELECT * FROM c"
            |> Cosmos.execAsync
            |> AsyncSeq.map (fun f -> { f with LastName = "Powellz" })
            |> AsyncSeq.map (fun f -> conn |> Cosmos.replace f |> Cosmos.execAsync)
            |> AsyncSeq.iter (fun f -> printfn $"Replaced: %A{f}")

        // do!
        //     conn
        //     |> Cosmos.container "Family"
        //     |> Cosmos.deleteContainer
        //     |> Cosmos.execAsync
        //     |> Async.Ignore

        do!
            conn
            |> Cosmos.deleteContainerIfExists
            |> Cosmos.execAsync
            |> Async.Ignore

        return 0 // return an integer exit code
    }
    |> Async.RunSynchronously
