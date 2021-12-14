[<AutoOpen>]
module TestUtils

open System

let inline find file =
    IO.Path.Combine(__SOURCE_DIRECTORY__, file)

let inline context file = AnalyzerBootstrap.context file
