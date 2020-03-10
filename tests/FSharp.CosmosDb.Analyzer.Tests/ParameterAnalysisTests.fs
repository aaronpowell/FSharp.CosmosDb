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
          } ]
