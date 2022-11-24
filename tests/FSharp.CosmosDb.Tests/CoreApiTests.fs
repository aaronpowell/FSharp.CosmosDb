namespace FSharp.CosmosDb.Tests
    module ``Create base resources`` =
        open Microsoft.Azure.Cosmos
        open FSharp.CosmosDb
        open FSharp.Control
        open Xunit
        open FsUnit.Xunit
        open Microsoft.Extensions.Configuration
        open System.IO
        open System

        let environmentName =
            match System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") with
            | "" | null -> "Development"
            | _ -> System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")

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

        type ``Create database``() =
            let databaseId = Guid.NewGuid().ToString()
            let host = config.["Cosmos:EndPoint"]
            let key = config.["Cosmos:Key"]

            let [<Fact>] ``Can create database if not exists``() =
                async {
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
                          properties.Id |> should equal databaseId)
                }

            interface IDisposable with
                override _.Dispose() =
                    use client = new CosmosClient(host, key)
                    client.GetDatabase(databaseId).DeleteAsync()
                    |> Async.AwaitTask
                    |> Async.RunSynchronously
                    |> ignore

        type TestType =
            { [<Id>]
              Id: string
              [<PartitionKey>]
              PK: string }

        type ``Container create``() =
            let host = config.["Cosmos:EndPoint"]
            let key = config.["Cosmos:Key"]

            let databaseId = Guid.NewGuid().ToString()
            let containerName = Guid.NewGuid().ToString()
            let [<Fact>] ``Create container if not exists`` () = 
                async {
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
                            properties.Id  |> should equal conn.DatabaseId.Value)

                    do!
                        conn
                        |> Cosmos.createContainerIfNotExists
                        |> Cosmos.execAsync<TestType>
                        |> AsyncSeq.iter (fun properties -> 
                        properties.Id |>should equal containerName)
            }

            interface IDisposable with
                override _.Dispose() =
                    use client = new CosmosClient(host, key)
                    client.GetDatabase(databaseId).DeleteAsync()
                    |> Async.AwaitTask
                    |> Async.RunSynchronously
                    |> ignore
