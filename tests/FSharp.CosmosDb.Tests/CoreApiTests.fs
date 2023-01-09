namespace FSharp.CosmosDb.Tests

module ``Create base resources`` =
    open Microsoft.Azure.Cosmos
    open FSharp.CosmosDb
    open FSharp.Control
    open Xunit
    open FsUnit.Xunit
    open System

    let config = Config.config

    type ``Create database``() =
        let databaseId = Guid.NewGuid().ToString()
        let host = config.["Cosmos:EndPoint"]
        let key = config.["Cosmos:Key"]

        [<Fact>]
        let ``Can create database if not exists`` () =
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
                    |> AsyncSeq.iter (fun properties -> properties.Id |> should equal databaseId)
            }

        [<Fact>]
        let ``Can create database`` () =
            async {
                use conn =
                    host
                    |> Cosmos.host
                    |> Cosmos.connect key
                    |> Cosmos.database databaseId

                do!
                    conn
                    |> Cosmos.createDatabase
                    |> Cosmos.execAsync
                    |> AsyncSeq.iter (fun properties -> properties.Id |> should equal databaseId)
            }

        [<Fact>]
        let ``Create database fails if db already exists`` () =
            async {
                use conn =
                    host
                    |> Cosmos.host
                    |> Cosmos.connect key
                    |> Cosmos.database databaseId

                do!
                    conn
                    |> Cosmos.createDatabase
                    |> Cosmos.execAsync
                    |> AsyncSeq.iter (fun properties -> properties.Id |> should equal databaseId)

                (fun () ->
                    conn
                    |> Cosmos.createDatabase
                    |> Cosmos.execAsync
                    |> AsyncSeq.iter (fun _ -> failwith "Shouldn't get here")
                    |> Async.RunSynchronously
                    |> ignore)
                |> should throw typeof<Exception>
            }

        [<Fact>]
        let ``Create database wont fail if exists using createDatabaseIfNotExists`` () =
            async {
                use conn =
                    host
                    |> Cosmos.host
                    |> Cosmos.connect key
                    |> Cosmos.database databaseId

                do!
                    conn
                    |> Cosmos.createDatabase
                    |> Cosmos.execAsync
                    |> AsyncSeq.iter (fun properties -> properties.Id |> should equal databaseId)

                do!
                    conn
                    |> Cosmos.createDatabaseIfNotExists
                    |> Cosmos.execAsync
                    |> AsyncSeq.iter (fun properties -> properties.Id |> should equal databaseId)
            }

        interface IDisposable with
            override _.Dispose() =
                use client = new CosmosClient(host, key)

                client.GetDatabase(databaseId).DeleteAsync()
                |> Async.AwaitTask
                |> Async.RunSynchronously
                |> ignore

    type ``Container create``() =
        let host = config.["Cosmos:EndPoint"]
        let key = config.["Cosmos:Key"]

        let databaseId = Guid.NewGuid().ToString()
        let containerName = Guid.NewGuid().ToString()

        [<Fact>]
        let ``Create container if not exists`` () =
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
                        properties.Id
                        |> should equal conn.DatabaseId.Value)

                do!
                    conn
                    |> Cosmos.createContainerIfNotExists
                    |> Cosmos.execAsync<TestType>
                    |> AsyncSeq.iter (fun properties -> properties.Id |> should equal containerName)
            }

        [<Fact>]
        let ``Create container`` () =
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
                        properties.Id
                        |> should equal conn.DatabaseId.Value)

                do!
                    conn
                    |> Cosmos.createContainer
                    |> Cosmos.execAsync<TestType>
                    |> AsyncSeq.iter (fun properties -> properties.Id |> should equal containerName)
            }

        [<Fact>]
        let ``Create container fails if container exists`` () =
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
                        properties.Id
                        |> should equal conn.DatabaseId.Value)

                do!
                    conn
                    |> Cosmos.createContainer
                    |> Cosmos.execAsync<TestType>
                    |> AsyncSeq.iter (fun properties -> properties.Id |> should equal containerName)

                (fun () ->
                    conn
                    |> Cosmos.createContainer
                    |> Cosmos.execAsync<TestType>
                    |> AsyncSeq.iter (fun _ -> failwith "should not get here")
                    |> Async.RunSynchronously)
                |> should throw typeof<Exception>
            }

        [<Fact>]
        let ``Create container won't fail if container exists using createContainerIfNotExists`` () =
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
                        properties.Id
                        |> should equal conn.DatabaseId.Value)

                do!
                    conn
                    |> Cosmos.createContainer
                    |> Cosmos.execAsync<TestType>
                    |> AsyncSeq.iter (fun properties -> properties.Id |> should equal containerName)

                do!
                    conn
                    |> Cosmos.createContainerIfNotExists
                    |> Cosmos.execAsync<TestType>
                    |> AsyncSeq.iter (fun properties -> properties.Id |> should equal containerName)
            }

        [<Fact>]
        let ``Create container sets partition key from attribute`` () =
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
                        properties.Id
                        |> should equal conn.DatabaseId.Value)

                do!
                    conn
                    |> Cosmos.createContainer
                    |> Cosmos.execAsync<TestType>
                    |> AsyncSeq.iter (fun properties -> properties.Id |> should equal containerName)


                let container = Cosmos.Raw.container conn

                match container with
                | Some container ->
                    task {
                        let! res = container.ReadContainerAsync()
                        return res.Resource.PartitionKeyPath
                    }
                    |> Async.AwaitTask
                    |> Async.RunSynchronously
                    |> should equal "/PK"
                | None -> failwith "Should have got a container"
            }

        interface IDisposable with
            override _.Dispose() =
                use client = new CosmosClient(host, key)

                client.GetDatabase(databaseId).DeleteAsync()
                |> Async.AwaitTask
                |> Async.RunSynchronously
                |> ignore
