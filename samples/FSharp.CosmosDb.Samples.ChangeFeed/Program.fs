open FSharp.CosmosDb
open FSharp.Control
open Microsoft.Extensions.Configuration
open System.IO
open Types
open System

let getFamiliesConnection host key =
    host
    |> Cosmos.host
    |> Cosmos.connect key
    |> Cosmos.database "FamilyDatabase"
    |> Cosmos.container "FamilyContainer"

[<EntryPoint>]
let main argv =
    let environmentName =
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")

    let builder =
        JsonConfigurationExtensions.AddJsonFile(
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

    let config = builder.Build()

    async {
        let host = config.["CosmosConnection:Host"]
        let key = config.["CosmosConnection:Key"]
        let conn = getFamiliesConnection host key

        let onChange changes _ =
            printfn $"Changes: %A{changes}"
            System.Threading.Tasks.Task.CompletedTask

        let processor =
            conn
            |> Cosmos.ChangeFeed.create<Family> "changeFeedSample" onChange
            |> Cosmos.ChangeFeed.withInstanceName "consoleHost"
            |> Cosmos.ChangeFeed.leaseContainer (conn |> Cosmos.container "leaseContainer")
            |> Cosmos.ChangeFeed.build

        printfn "Starting change feed"
        do! processor.StartAsync() |> Async.AwaitTask

        printfn "Change feed started. Press any key to exit"

        Console.Read() |> ignore

        return 0 // return an integer exit code
    }
    |> Async.RunSynchronously
