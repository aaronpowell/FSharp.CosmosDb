module Tests

open FSharp.CosmosDb.Analyzer
open FsUnit.Xunit
open Xunit

type ``Connection object discovery``() =
    [<Fact>]
    let ``Finds all the operations in a file``() =
        match context (find "../samples/simpleSample.fs") with
        | None -> failwith "Could not load test script"
        | Some context ->
            let ops = CosmosCodeAnalysis.findOperations context
            ops |> should haveLength 1

    [<Fact>]
    let ``Found operation should have 2 analysable bits``() =
        match context (find "../samples/simpleSample.fs") with
        | None -> failwith "Could not load test script"
        | Some context ->
            let ops = CosmosCodeAnalysis.findOperations context
            let head = List.exactlyOne ops
            head.blocks |> should haveLength 2

    [<Fact>]
    let ``DatabaseId should match expected``() =
        match context (find "../samples/simpleSample.fs") with
        | None -> failwith "Could not load test script"
        | Some context ->
            let ops = CosmosCodeAnalysis.findOperations context
            let head = List.exactlyOne ops

            let dbId =
                head.blocks
                |> List.tryFind (function
                    | CosmosAnalyzerBlock.DatabaseId (_) -> true
                    | _ -> false)
                |> Option.map (function
                    | CosmosAnalyzerBlock.DatabaseId (dbId, _) -> dbId
                    | _ -> failwith "Should've found the DatabaseId operation")

            dbId.Value |> should equal "UserDb"

    [<Fact>]
    let ``ContainerName should match expected``() =
        match context (find "../samples/simpleSample.fs") with
        | None -> failwith "Could not load test script"
        | Some context ->
            let ops = CosmosCodeAnalysis.findOperations context
            let head = List.exactlyOne ops

            let container =
                head.blocks
                |> List.tryFind (function
                    | CosmosAnalyzerBlock.ContainerName (_) -> true
                    | _ -> false)
                |> Option.map (function
                    | CosmosAnalyzerBlock.ContainerName (containerName, _) -> containerName
                    | _ -> failwith "Should've found the ContainerName operation")

            container.Value |> should equal "UserContainer"
