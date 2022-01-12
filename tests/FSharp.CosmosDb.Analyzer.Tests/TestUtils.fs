[<AutoOpen>]
module TestUtils

open System

let inline find file =
    // let basePath = match Environment.GetEnvironmentVariable("CI") with
    //                | null -> __SOURCE_DIRECTORY__
    //                | _ -> IO.Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE"), "tests", "FSharp.CosmosDb.Analyzer.Tests")
    // IO.Path.Combine(basePath, file)
    IO.Path.Combine(__SOURCE_DIRECTORY__, file)

let inline context file = AnalyzerBootstrap.context file
