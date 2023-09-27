module internal ConnectionLookup

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

let private gev key =
    let var = Environment.GetEnvironmentVariable key
    if isNull var || String.IsNullOrWhiteSpace var
    then None
    else Some var

let private makeConnStr =
    sprintf "AccountEndpoint=%s;AccountKey=%s;"

let private stringHasValue str =
    not (isNull str || String.IsNullOrEmpty str)

let private findFromEnv () =
    let host = gev "FSHARP_COSMOS_HOST"
    let key = gev "FSHARP_COSMOS_KEY"
    let cs = gev "FSHARP_COSMOS_CONNSTR"

    match host, key, cs with
    | Some host, Some key, None ->
        $"AccountEndpoint=%s{host};AccountKey=%s{key};"
        |> Some
    | None, None, cs -> cs
    | _ -> None

let private possibleFileNames =
    [ "appsettings.json"
      "appsettings.Development.json" ]

type AppSettings =
    { CosmosConnection: {| Host: String option
                           Key: String option
                           ConnectionString: String option |} option }

let private findFromAppSettings relativeFile =
    let folders =
        [ Environment.CurrentDirectory
          FileInfo(relativeFile).Directory.FullName ]

    let settingsFiles =
        possibleFileNames
        |> List.map (fun file ->
            folders
            |> List.map (fun folder -> Path.Combine(folder, file)))
        |> List.append
            (possibleFileNames
             |> List.map (fun file ->
                 folders
                 |> List.map (fun folder -> Path.Combine(folder, file.ToLower()))))
        |> List.collect id
        |> List.filter File.Exists

    let options = JsonSerializerOptions()
    options.Converters.Add(JsonFSharpConverter())

    let parsedConfig =
        settingsFiles
        |> List.map (fun file ->
            let contents = File.ReadAllText file
            JsonSerializer.Deserialize<AppSettings>(contents, options))
        |> List.filter (fun config ->
            match config.CosmosConnection with
            | Some cc ->
                match cc.ConnectionString, cc.Host, cc.Key with
                | Some cs, None, None when stringHasValue cs -> true
                | Some cs, Some _, Some _ when stringHasValue cs -> true
                | Some _, Some host, Some key when stringHasValue host && stringHasValue key -> true
                | None, Some host, Some key when stringHasValue host && stringHasValue key -> true
                | _, _, _ -> false
            | None -> false)
        |> List.tryHead

    match parsedConfig with
    | None -> None
    | Some config ->
        match config.CosmosConnection with
        | Some cc ->
            match cc.ConnectionString, cc.Host, cc.Key with
            | Some cs, None, None when stringHasValue cs -> Some cs
            | Some cs, Some _, Some _ when stringHasValue cs -> Some cs
            | Some _, Some host, Some key when stringHasValue host && stringHasValue key ->
                makeConnStr host key |> Some
            | None, Some host, Some key when stringHasValue host && stringHasValue key -> makeConnStr host key |> Some
            | _, _, _ -> None
        | None -> None

let findConnectionString relativeFile =
    match findFromEnv () with
    | Some connStr -> Some connStr
    | None ->
        match findFromAppSettings relativeFile with
        | Some connStr -> Some connStr
        | None -> None
