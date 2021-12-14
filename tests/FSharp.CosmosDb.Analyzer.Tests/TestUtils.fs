[<AutoOpen>]
module TestUtils

open System

let find file =
    IO.Path.Combine(__SOURCE_DIRECTORY__, file)

let context file = AnalyzerBootstrap.context file
