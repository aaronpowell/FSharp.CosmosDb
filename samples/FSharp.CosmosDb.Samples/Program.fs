module FSharp.CosmosDb.Samples

open FSharp.CosmosDb
open System.Text.Json.Serialization
open FSharp.Control
open Microsoft.Extensions.Configuration
open System.IO

[<CLIMutable>]
type Parent =
    { FamilyName: string
      FirstName: string }

[<CLIMutable>]
type Pet =
    { GivenName: string }

[<CLIMutable>]
type Child =
    { FamilyName: string
      FirstName: string
      Gender: string
      Grade: int
      Pets: Pet array }

[<CLIMutable>]
type Address =
    { State: string
      Country: string
      City: string }

[<CLIMutable>]
type Family =
    { [<JsonPropertyName("id")>]
      Id: string
      LastName: string
      IsRegistered: bool
      Parents: Parent array
      Children: Child array
      Address: Address }

let getFamilies host key =
    Cosmos.host host
    |> Cosmos.connect key
    |> Cosmos.database "FamilyDatabaseA"
    |> Cosmos.container "FamilyContainerB"
    |> Cosmos.query "SELECT * FROM f WHERE f.Name = @name"
    |> Cosmos.parameters [ "age", box 35 ]
    |> Cosmos.execAsync<Family>

[<EntryPoint>]
let main argv =
    let environmentName = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    let builder =
        JsonConfigurationExtensions.AddJsonFile
            (JsonConfigurationExtensions.AddJsonFile
                (FileConfigurationExtensions.SetBasePath(ConfigurationBuilder(), Directory.GetCurrentDirectory()),
                 "appsettings.json", true, true), sprintf "appsettings.%s.json" environmentName, true, true)

    let config = builder.Build()

    async {
        let host = config.["CosmosConnection:Host"]
        let key = config.["CosmosConnection:Key"]

        let families = getFamilies host key

        do! families |> AsyncSeq.iter (fun f -> printfn "%A" f)

        return 0 // return an integer exit code
    }
    |> Async.RunSynchronously
