module ParameterAnalysisTests

open Expecto
open FSharp.CosmosDb.Analyzer
open FSharp.Compiler.Text

[<Tests>]
let tests =
    testList
        "Parameters"
        [ test "All parameters matching returns no messages" {
            let query = "SELECT * FROM u WHERE u.Name = @name"

            let queryOperation =
                { blocks = [ CosmosAnalyzerBlock.Query(query, range.Zero) ]
                  range = range.Zero }

            let parameters =
                [ { name = "@name"
                    range = range.Zero
                    paramFunc = ""
                    paramFuncRange = range.Zero
                    applicationRange = None } ]

            let msgs =
                CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

            Expect.equal 0 msgs.Length "All parameters matched"
          }

          test "If parameter in query not in provided a warning is raised" {
              let query = "SELECT * FROM u WHERE u.Name = @name"

              let queryOperation =
                  { blocks = [ CosmosAnalyzerBlock.Query(query, range.Zero) ]
                    range = range.Zero }

              let parameters = []

              let msgs =
                  CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

              Expect.equal 1 msgs.Length "One parameter wasn't provided"
          }

          test "If parameter provided but not used a warning is raised" {
              let query = "SELECT * FROM u"

              let queryOperation =
                  { blocks = [ CosmosAnalyzerBlock.Query(query, range.Zero) ]
                    range = range.Zero }

              let parameters =
                  [ { name = "@name"
                      range = range.Zero
                      paramFunc = ""
                      paramFuncRange = range.Zero
                      applicationRange = None } ]

              let msgs =
                  CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

              Expect.equal 1 msgs.Length "Too many parameters provided"
          }

          test "Parameter name missmatch deteched" {
              let query = "SELECT * FROM u WHERE u.Name = @NAME"

              let queryOperation =
                  { blocks = [ CosmosAnalyzerBlock.Query(query, range.Zero) ]
                    range = range.Zero }

              let parameters =
                  [ { name = "@name"
                      range = range.Zero
                      paramFunc = ""
                      paramFuncRange = range.Zero
                      applicationRange = None } ]

              let msgs =
                  CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

              Expect.equal 2 msgs.Length "Defined parameter not provided and provided parameter not used"
          }

          test "Params in query offered fix of defined" {
              let query = "SELECT * FROM u WHERE u.Name = @NAME"

              let queryOperation =
                  { blocks = [ CosmosAnalyzerBlock.Query(query, range.Zero) ]
                    range = range.Zero }

              let parameters =
                  [ { name = "@name"
                      range = range.Zero
                      paramFunc = ""
                      paramFuncRange = range.Zero
                      applicationRange = None } ]

              let msgs =
                  CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

              let queryParamMsg =
                  msgs
                  |> List.find (fun msg -> msg.Code = "CDB1003")

              let fix =
                  queryParamMsg.Fixes |> List.tryExactlyOne

              Expect.isSome fix "One fix was found for NAME"
              Expect.equal fix.Value.ToText "@name" "ToText matches provided parameter"
              Expect.equal fix.Value.FromText "@NAME" "FromText matches used parameter"
          }

          test "Provided params offed fix for used params" {
              let query = "SELECT * FROM u WHERE u.Name = @NAME"

              let queryOperation =
                  { blocks = [ CosmosAnalyzerBlock.Query(query, range.Zero) ]
                    range = range.Zero }

              let parameters =
                  [ { name = "@name"
                      range = range.Zero
                      paramFunc = ""
                      paramFuncRange = range.Zero
                      applicationRange = None } ]

              let msgs =
                  CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

              let queryParamMsg =
                  msgs
                  |> List.find (fun msg -> msg.Message.Contains "name")

              let fix =
                  queryParamMsg.Fixes |> List.tryExactlyOne

              Expect.isSome fix "One fix was found for name"
              Expect.equal fix.Value.ToText "\"@NAME\"" "ToText matches provided parameter"
              Expect.equal fix.Value.FromText "@name" "FromText matches used parameter"
          }

          test "Muliple params in query are fix options" {
              let query =
                  "SELECT * FROM u WHERE u.Name = @NAME AND u.Age = @age"

              let queryOperation =
                  { blocks = [ CosmosAnalyzerBlock.Query(query, range.Zero) ]
                    range = range.Zero }

              let parameters =
                  [ { name = "@name"
                      range = range.Zero
                      paramFunc = ""
                      paramFuncRange = range.Zero
                      applicationRange = None }
                    { name = "@age"
                      range = range.Zero
                      paramFunc = ""
                      paramFuncRange = range.Zero
                      applicationRange = None } ]

              let msgs =
                  CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

              Expect.hasLength msgs 2 "Two errors between query and params"

              let queryParamMsg =
                  msgs
                  |> List.find (fun msg -> msg.Message.Contains "@name")

              Expect.hasLength queryParamMsg.Fixes 2 "Two options to fix"
          }

          test "Params without @ at start are offered a fix" {
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

              let msgs =
                  CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

              let queryParamMsg =
                  msgs
                  |> List.find (fun msg -> msg.Code = Messaging.ParameterMissingSymbol.Code)

              let fix = queryParamMsg.Fixes |> Seq.tryExactlyOne

              Expect.isSome fix $"A fix exists for %s{Messaging.ParameterMissingSymbol.Code}"
              Expect.equal fix.Value.FromText "name" "Starts from the parameter name"
              Expect.equal fix.Value.ToText "\"@name\"" "Replacement contains @"
          }

          test "Params with @ at start aren't offered a fix" {
              let query = "SELECT * FROM u WHERE u.Name = @name"

              let queryOperation =
                  { blocks = [ CosmosAnalyzerBlock.Query(query, range.Zero) ]
                    range = range.Zero }

              let parameters =
                  [ { name = "@name"
                      range = range.Zero
                      paramFunc = ""
                      paramFuncRange = range.Zero
                      applicationRange = None } ]

              let msgs =
                  CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

              Expect.hasLength msgs 0 "No fixes required"
          } ]
