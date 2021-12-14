module QueryTests

open Expecto
open System
open FSharp.CosmosDb.Analyzer

[<Tests>]
let tests =
    testList
        "Query API can be analyzed"
        [ test "Finds all the operations in a file" {
              printfn "Line: %s" __LINE__
              printfn "Source Directory: %s" __SOURCE_DIRECTORY__
              printfn "Source File: %s" __SOURCE_FILE__

              match context (find "../samples/querySample.fs") with
              | None -> failwith "Could not load test script"
              | Some context ->
                  let ops = CosmosCodeAnalysis.findOperations context

                  Expect.equal 1 (List.length ops) "Found one operation block"
          }

          test "Found operation should have 3 analysable bits" {
              match context (find "../samples/querySample.fs") with
              | None -> failwith "Could not load test script"
              | Some context ->
                  let ops = CosmosCodeAnalysis.findOperations context

                  let head = List.exactlyOne ops
                  Expect.equal 4 (List.length head.blocks) "Found four things to analyse"
          }

          test "Query should match expected" {
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

                  Expect.equal "SELECT * FROM u WHERE u.Name = @name" query.Value "Query matches the one in code"
          }

          test "DatabaseId should match expected" {
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

                  Expect.equal "UserDb" dbId.Value "DatabaseId matches the one in code"
          }

          test "ContainerName should match expected" {
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

                  Expect.equal "UserContainer" container.Value "ContainerName matches the one in code"
          }

          test "Parameter should match expected" {
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
                      Expect.equal 1 p.Length "Only 1 parameter was defined"
                      let nameParam = List.exactlyOne p
                      Expect.equal "name" nameParam.name "Parameter key is 'name'"
                  | None -> failwith "Parameters not found"
          } ]
