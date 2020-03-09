module CosmosDbAnalyzer

open FSharp.Analyzers.SDK
open FSharp.CosmosDb.Analyzer

[<Analyzer>]
let cosmosDbAnalyzer: Analyzer =
    fun (context: Context) ->
        let syntaxBlocks = CosmosCodeAnalysis.findOperations context
        []
