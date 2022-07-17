﻿module FSharp.CosmosDb.Samples

open FSharp.CosmosDb
open FSharp.Control
open Microsoft.Extensions.Configuration
open System.IO
open Types

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

let getParents conn =
    conn
    |> Cosmos.linq<Family, Parent[]> (fun families ->
        cosmosQuery {
            for family in families do
            where (family.LastName = "Powell")
            select family.Parents
        })
    |> Cosmos.execAsync

let updateFamily conn id pk =
    conn
    |> Cosmos.update<Family>
        id
        pk
        (fun family ->
            { family with
                  IsRegistered = not family.IsRegistered })
    |> Cosmos.execAsync

let deleteFamily conn id pk =
    conn
    |> Cosmos.delete<Family> id pk
    |> Cosmos.execAsync

[<EntryPoint>]
let main argv =
    let environmentName =
        System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")

    let builder =
        JsonConfigurationExtensions.AddJsonFile(
            JsonConfigurationExtensions.AddJsonFile(
                FileConfigurationExtensions.SetBasePath(ConfigurationBuilder(), Directory.GetCurrentDirectory()),
                "appsettings.json",
                true,
                true
            ),
            sprintf "appsettings.%s.json" environmentName,
            true,
            true
        )

    let config = builder.Build()

    async {
        let host = config.["CosmosConnection:Host"]
        let key = config.["CosmosConnection:Key"]
        use conn = getFamiliesConnection host key

        // let connectionString =
        //     config.["CosmosConnection:ConnectionString"]

        // let conn =
        //     getFamiliesConnectionFromConnString connectionString

        printfn "Getting ready to do some Cosmos operations"

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
            |> AsyncSeq.iter (fun f -> printfn "Inserted: %A" f)

        let families = getFamilies conn

        do!
            families
            |> AsyncSeq.iter (fun f -> printfn "Got: %A" f)

        let firstNames = getParents conn

        do!
            firstNames
            |> AsyncSeq.iter (printfn "Got: %A") 

        let updatePowell = updateFamily conn "Powell.1" "Powell"

        do!
            updatePowell
            |> AsyncSeq.iter (fun f -> printfn "Updated: %A" f)

        let deletePowell = deleteFamily conn "Powell.1" "Powell"

        do!
            deletePowell
            |> AsyncSeq.iter (fun f -> printfn "Deleted: %A" f)

        do!
            conn
            |> Cosmos.query<Family> "SELECT * FROM c"
            |> Cosmos.execAsync
            |> AsyncSeq.map (fun f -> { f with LastName = "Powellz" })
            |> AsyncSeq.map (fun f -> conn |> Cosmos.replace f |> Cosmos.execAsync)
            |> AsyncSeq.iter (fun f -> printfn "Replaced: %A" f)

        return 0 // return an integer exit code
    }
    |> Async.RunSynchronously
