module CosmosDbAnalyzer

open FSharp.Analyzers.SDK
open FSharp.CosmosDb.Analyzer

[<Analyzer>]
let cosmosDbAnalyzer: Analyzer =
    fun (context: Context) ->
        let syntaxBlocks = CosmosCodeAnalysis.findOperations context

        let connStr = ConnectionLookup.findConnectionString context.FileName

        match connStr with
        | Some connStr ->
            match CosmosCodeAnalyzer.testConnection connStr with
            | Error msg ->
                [ for block in syntaxBlocks -> Messaging.error msg block.range ]
            | Success client ->
                syntaxBlocks
                |> List.collect (fun block ->
                    match CosmosCodeAnalyzer.findDatabaseOperation block with
                    | Some(dbId, range) ->
                        CosmosCodeAnalyzer.analyzeDatabaseOperation dbId range client
                        |> List.append
                            (match CosmosCodeAnalyzer.findContainerOperation block with
                             | Some(containerName, range) ->
                                 CosmosCodeAnalyzer.analyzeContainerNameOperation dbId containerName range client
                             | None -> [])
                        |> List.append
                            (match CosmosCodeAnalyzer.findParameters block with
                             | Some(parameters, range) -> CosmosCodeAnalyzer.analyzeParameters block parameters range
                             | None -> [])

                    | None ->
                        match CosmosCodeAnalyzer.findParameters block with
                        | Some(parameters, range) -> CosmosCodeAnalyzer.analyzeParameters block parameters range
                        | None -> [])
                |> List.distinctBy (fun msg -> msg.Range)
        | None ->
            [ for block in syntaxBlocks ->
                Messaging.warning
                    "No connection string was found, ensure either the appropriate environment variables or appsettings.json is created"
                    block.range ]
