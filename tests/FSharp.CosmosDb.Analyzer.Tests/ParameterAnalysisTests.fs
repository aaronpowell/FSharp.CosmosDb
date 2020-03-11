module ParameterAnalysisTests

open Expecto
open FSharp.CosmosDb.Analyzer
open FSharp.Compiler.Range

[<Tests>]
let tests =
    testList "Parameters"
        [ test "All parameters matching returns no messages" {
              let query = "SELECT * FROM u WHERE u.Name = @name"

              let queryOperation =
                  { blocks = [ CosmosAnalyzerBlock.Query(query, range.Zero) ]
                    range = range.Zero }

              let parameters =
                  [ { name = "name"
                      range = range.Zero
                      paramFunc = ""
                      paramFuncRange = range.Zero
                      applicationRange = None } ]

              let msgs = CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

              Expect.equal 0 msgs.Length "All parameters matched"
          }

          test "If parameter in query not in provided a warning is raised" {
              let query = "SELECT * FROM u WHERE u.Name = @name"

              let queryOperation =
                  { blocks = [ CosmosAnalyzerBlock.Query(query, range.Zero) ]
                    range = range.Zero }

              let parameters = []

              let msgs = CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

              Expect.equal 1 msgs.Length "One parameter wasn't provided"
          }

          test "If parameter provided but not used a warning is raised" {
              let query = "SELECT * FROM u"

              let queryOperation =
                  { blocks = [ CosmosAnalyzerBlock.Query(query, range.Zero) ]
                    range = range.Zero }

              let parameters =
                  [ { name = "name"
                      range = range.Zero
                      paramFunc = ""
                      paramFuncRange = range.Zero
                      applicationRange = None } ]

              let msgs = CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

              Expect.equal 1 msgs.Length "Too many parameters provided"
          }

          test "Parameter name missmatch deteched" {
              let query = "SELECT * FROM u WHERE u.Name = @NAME"

              let queryOperation =
                  { blocks = [ CosmosAnalyzerBlock.Query(query, range.Zero) ]
                    range = range.Zero }

              let parameters =
                  [ { name = "name"
                      range = range.Zero
                      paramFunc = ""
                      paramFuncRange = range.Zero
                      applicationRange = None } ]

              let msgs = CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

              Expect.equal 2 msgs.Length "Defined parameter not provided and provided parameter not used"
          } ]
