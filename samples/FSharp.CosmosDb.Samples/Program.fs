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
      [<PartitionKey>]
      LastName: string
      IsRegistered: bool
      Parents: Parent array
      Children: Child array
      Address: Address }

let getFamiliesConnection host key =
    host
    |> Cosmos.host
    |> Cosmos.connect key
    |> Cosmos.database "FamilyDatabase"
    |> Cosmos.container "FamilyContainer"

let getFamiliesConnectionFromConnString connectionString =
    connectionString
    |> Cosmos.fromConnectionString
    |> Cosmos.database "FamilyDatabase"
    |> Cosmos.container "FamilyContainer"

let insertFamilies<'T> conn (families: 'T list) =
    conn
    |> Cosmos.insertMany<'T> families
    |> Cosmos.execAsync<'T>

// this is to test a broken query in the analyzer
let getFamiliesBroken conn =
    conn
    |> Cosmos.query "SELECT * FROM f WHERE f.Name = @name AND f.Age = @AGE"
    |> Cosmos.parameters
        [ "age", box 35
          "lastName", box "Powell" ]
    |> Cosmos.execAsync<Family>

let getFamilies conn =
    conn
    |> Cosmos.query "SELECT * FROM f WHERE f.LastName = @lastName"
    |> Cosmos.parameters [ "@lastName", box "Powell" ]
    |> Cosmos.execAsync<Family>

let updateFamily conn id pk =
    conn
    |> Cosmos.updateItem<Family> id pk (fun family -> { family with IsRegistered = not family.IsRegistered })
    |> Cosmos.execAsync

let deleteFamily conn id pk =
    conn
    |> Cosmos.deleteItem<Family> id pk
    |> Cosmos.execAsync

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
        // let host = config.["CosmosConnection:Host"]
        // let key = config.["CosmosConnection:Key"]
        // let conn = getFamiliesConnection host key

        let connectionString = config.["CosmosConnection:ConnectionString"]

        let conn = getFamiliesConnectionFromConnString connectionString

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
        do! insert |> AsyncSeq.iter (fun f -> printfn "Inserted: %A" f)

        let families = getFamilies conn
        do! families |> AsyncSeq.iter (fun f -> printfn "Got: %A" f)

        let updatePowell = updateFamily conn "Powell.1" "Powell"
        do! updatePowell |> AsyncSeq.iter (fun f -> printfn "Updated: %A" f)

        let deletePowell = deleteFamily conn "Powell.1" "Powell"
        do! deletePowell |> AsyncSeq.iter (fun f -> printfn "Deleted: %A" f)

        return 0 // return an integer exit code
    }
    |> Async.RunSynchronously
