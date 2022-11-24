module Tests

open Expecto
open Microsoft.Extensions.Configuration
open System.IO
open FSharp.CosmosDb
open FSharp.Control
open System

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
            sprintf "appsettings.%s.json" environmentName,
            true,
            true
        )
        .AddEnvironmentVariables()

let config = builder.Build()

type TestType =
    { [<Id>]
      Id: string
      [<PartitionKey>]
      PK: string }

let tests =
    testList
        "Tests"
        [ testAsync "Can create database" {
              let host = config.["Cosmos:EndPoint"]
              let key = config.["Cosmos:Key"]

              let databaseId = Guid.NewGuid().ToString()

              use conn =
                  host
                  |> Cosmos.host
                  |> Cosmos.connect key
                  |> Cosmos.database databaseId

              do!
                  conn
                  |> Cosmos.createDatabaseIfNotExists
                  |> Cosmos.execAsync
                  |> AsyncSeq.iter (fun properties ->
                      Expect.equal properties.Id databaseId "Database ID didn't match expected")
          }

          testAsync "Can create container" {
              let host = config.["Cosmos:EndPoint"]
              let key = config.["Cosmos:Key"]

              let databaseId = Guid.NewGuid().ToString()
              let containerName = Guid.NewGuid().ToString()`

              use conn =
                  host
                  |> Cosmos.host
                  |> Cosmos.connect key
                  |> Cosmos.database databaseId
                  |> Cosmos.container containerName

              do!
                  conn
                  |> Cosmos.createDatabaseIfNotExists
                  |> Cosmos.execAsync
                  |> AsyncSeq.iter (fun properties ->
                      Expect.equal properties.Id conn.DatabaseId.Value "Database ID didn't match expected")

              do!
                  conn
                  |> Cosmos.createContainerIfNotExists
                  |> Cosmos.execAsync<TestType>
                  |> AsyncSeq.iter (fun properties -> Expect.equal properties.Id containerName "Container name matched")
          } ]
