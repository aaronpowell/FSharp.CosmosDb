module QueryTests

open System
open FSharp.CosmosDb.Analyzer
open FsUnit.Xunit
open Xunit

type ``Query API can be analyzed``() =
    [<Fact>]
    let ``Finds all the operations in a file`` =
        match context (find "../samples/querySample.fs") with
        | None -> failwith "Could not load test script"
        | Some context ->
            let ops = CosmosCodeAnalysis.findOperations context

            ops |> should haveLength 1

    [<Fact>]
    let ``Found operation should have 3 analysable bits`` =
        match context (find "../samples/querySample.fs") with
        | None -> failwith "Could not load test script"
        | Some context ->
            let ops = CosmosCodeAnalysis.findOperations context

            let head = List.exactlyOne ops
            head.blocks |> should haveLength 4

    [<Fact>]
    let ``Query should match expected`` =
        match context (find "../samples/querySample.fs") with
        | None -> failwith "Could not load test script"
        | Some context ->
            let ops = CosmosCodeAnalysis.findOperations context

            let head = List.exactlyOne ops

            let query =
                head.blocks
                |> List.tryFind (function
                    | CosmosAnalyzerBlock.Query (_) -> true
                    | _ -> false)
                |> Option.map (function
                    | CosmosAnalyzerBlock.Query (query, range) -> query
                    | _ -> failwith "Should've found the query operation")

            query.Value
            |> should equal "SELECT * FROM u WHERE u.Name = @name"

    [<Fact>]
    let ``DatabaseId should match expected`` =
        match context (find "../samples/querySample.fs") with
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
    let ``ContainerName should match expected`` =
        match context (find "../samples/querySample.fs") with
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

    [<Fact>]
    let ``Parameter should match expected`` =
        match context (find "../samples/querySample.fs") with
        | None -> failwith "Could not load test script"
        | Some context ->
            let ops = CosmosCodeAnalysis.findOperations context

            let head = List.exactlyOne ops

            let parameters =
                head.blocks
                |> List.tryFind (function
                    | CosmosAnalyzerBlock.Parameters (_) -> true
                    | _ -> false)
                |> Option.map (function
                    | CosmosAnalyzerBlock.Parameters (parameters, _) -> parameters
                    | _ -> failwith "Should've found the Parameters operation")

            match parameters with
            | Some p ->
                p |> should haveLength 1
                let nameParam = List.exactlyOne p
                nameParam.name |> should equal "name"
            | None -> failwith "Parameters not found"
