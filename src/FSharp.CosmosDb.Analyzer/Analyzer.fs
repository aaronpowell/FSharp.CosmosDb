module CosmosDbAnalyzer

open FSharp.Analyzers.SDK
open FSharp.CosmosDb.Analyzer
open System
open Azure.Cosmos

[<Analyzer>]
let cosmosDbAnalyzer: Analyzer =
    fun (context: Context) ->
        printfn "pwd: %s" Environment.CurrentDirectory

        let syntaxBlocks = CosmosCodeAnalysis.findOperations context

        let host = Environment.GetEnvironmentVariable "FSHARP_COSMOS_HOST"
        if isNull host || String.IsNullOrWhiteSpace host then
            [ for block in syntaxBlocks ->
                Messaging.warning
                    "Host for Cosmos DB wasn't set. Ensure there's an environment varialbe named 'FSHARP_COSMOS_HOST'"
                    block.range ]
        else
            let key = Environment.GetEnvironmentVariable "FSHARP_COSMOS_KEY"
            if isNull key || String.IsNullOrWhiteSpace key then
                [ for block in syntaxBlocks ->
                    Messaging.warning
                        "Access key for Cosmos DB wasn't set. Ensure there's an environment varialbe named 'FSHARP_COSMOS_KEY'"
                        block.range ]
            else
                match CosmosCodeAnalyzer.testConnection host key with
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
                                 | Some(parameters, range) ->
                                     CosmosCodeAnalyzer.analyzeParameters block parameters range
                                 | None -> [])

                        | None ->
                            match CosmosCodeAnalyzer.findParameters block with
                            | Some(parameters, range) -> CosmosCodeAnalyzer.analyzeParameters block parameters range
                            | None -> [])
                    |> List.distinctBy (fun msg -> msg.Range)
