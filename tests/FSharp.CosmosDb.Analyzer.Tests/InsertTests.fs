module InsertTests

open Expecto
open System
open FSharp.CosmosDb.Analyzer

let inline find file = IO.Path.Combine(__SOURCE_DIRECTORY__, file)
let inline context file = AnalyzerBootstrap.context file

[<Tests>]
let tests =
    testList "Insert API can be analyzed"
        [ test "Finds all the operations in a file" {
              match context (find "../samples/insertSample.fs") with
              | None -> failwith "Could not load test script"
              | Some context ->
                  let ops = CosmosCodeAnalysis.findOperations context
                  Expect.equal 1 (List.length ops) "Found one operation block"
          }

          test "Found operation should have correct number of analysable bits" {
              match context (find "../samples/insertSample.fs") with
              | None -> failwith "Could not load test script"
              | Some context ->
                  let ops = CosmosCodeAnalysis.findOperations context
                  let head = List.exactlyOne ops
                  Expect.equal 2 (List.length head.blocks) "Found two things to analyse"
          }

          test "DatabaseId should match expected" {
              match context (find "../samples/insertSample.fs") with
              | None -> failwith "Could not load test script"
              | Some context ->
                  let ops = CosmosCodeAnalysis.findOperations context
                  let head = List.exactlyOne ops

                  let dbId =
                      head.blocks
                      |> List.tryFind (function
                          | CosmosAnalyzerBlock.DatabaseId(_) -> true
                          | _ -> false)
                      |> Option.map (function
                          | CosmosAnalyzerBlock.DatabaseId(dbId, _) -> dbId
                          | _ -> failwith "Should've found the DatabaseId operation")

                  Expect.equal "UserDb" dbId.Value "DatabaseId matches the one in code"
          }

          test "ContainerName should match expected" {
              match context (find "../samples/insertSample.fs") with
              | None -> failwith "Could not load test script"
              | Some context ->
                  let ops = CosmosCodeAnalysis.findOperations context
                  let head = List.exactlyOne ops

                  let container =
                      head.blocks
                      |> List.tryFind (function
                          | CosmosAnalyzerBlock.ContainerName(_) -> true
                          | _ -> false)
                      |> Option.map (function
                          | CosmosAnalyzerBlock.ContainerName(containerName, _) -> containerName
                          | _ -> failwith "Should've found the ContainerName operation")

                  Expect.equal "UserContainer" container.Value "ContainerName matches the one in code"
          } ]
