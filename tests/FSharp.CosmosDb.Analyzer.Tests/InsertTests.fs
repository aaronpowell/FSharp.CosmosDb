module InsertTests

open FSharp.CosmosDb.Analyzer
open FsUnit.Xunit
open Xunit

type ``Insert API can be analyzed``() =
    [<Fact>]
    let ``Finds all the operations in a file``() =
        match context (find "../samples/insertSample.fs") with
        | None -> failwith "Could not load test script"
        | Some context ->
            let ops = CosmosCodeAnalysis.findOperations context
            1 |> should equal (List.length ops)

    [<Fact>]
    let ``Found operation should have correct number of analysable bits``() =
        match context (find "../samples/insertSample.fs") with
        | None -> failwith "Could not load test script"
        | Some context ->
            let ops = CosmosCodeAnalysis.findOperations context
            let head = List.exactlyOne ops
            2 |> should equal (List.length head.blocks)

    [<Fact>]
    let ``DatabaseId should match expected``() =
        match context (find "../samples/insertSample.fs") with
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

            "UserDb" |> should equal dbId.Value

    [<Fact>]
    let ``ContainerName should match expected``() =
        match context (find "../samples/insertSample.fs") with
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

            "UserContainer" |> should equal container.Value
