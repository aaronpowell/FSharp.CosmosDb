[<AutoOpen>]
module TestUtils

open System

let find file=
        let basePath =
            match Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") with
                | null -> __SOURCE_DIRECTORY__
                | _ -> IO.Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE"), "tests", "FSharp.CosmosDb.Analyzer.Tests")
        IO.Path.Combine(basePath, file)

let inline context file = AnalyzerBootstrap.context file
