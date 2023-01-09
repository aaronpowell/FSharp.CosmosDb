module ParameterAnalysisTests

open FSharp.CosmosDb.Analyzer
open FSharp.Compiler.Text

open FsUnit.Xunit
open FsUnit.CustomMatchers
open Xunit

type Paramerers() =
    [<Fact>]
    let ``All parameters matching returns no messages``() =
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

        let msgs = CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

        0 |> should equal msgs.Length

    [<Fact>]
    let ``If parameter in query not in provided a warning is raised``() =
        let query = "SELECT * FROM u WHERE u.Name = @name"

        let queryOperation =
            { blocks = [ CosmosAnalyzerBlock.Query(query, range.Zero) ]
              range = range.Zero }

        let parameters = []

        let msgs = CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

        1 |> should equal msgs.Length

    [<Fact>]
    let ``If parameter provided but not used a warning is raised``() =
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

        let msgs = CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

        1 |> should equal msgs.Length

    [<Fact>]
    let ``Parameter name missmatch deteched``() =
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

        let msgs = CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

        2 |> should equal msgs.Length

    [<Fact>]
    let ``Params in query offered fix of defined``() =
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

        let msgs = CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

        let queryParamMsg =
            msgs
            |> List.find (fun msg -> msg.Code = "CDB1003")

        let fix = queryParamMsg.Fixes |> List.tryExactlyOne

        fix |> should be (ofCase <@ Some @>)
        fix.Value.ToText |> should equal "@name"
        fix.Value.FromText |> should equal "@NAME"

    [<Fact>]
    let ``Provided params offed fix for used params``() =
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

        let msgs = CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

        let queryParamMsg =
            msgs
            |> List.find (fun msg -> msg.Message.Contains "name")

        let fix = queryParamMsg.Fixes |> List.tryExactlyOne

        fix |> should be (ofCase <@ Some @>)
        fix.Value.ToText |> should equal "\"@NAME\""
        fix.Value.FromText |> should equal "@name"

    [<Fact>]
    let ``Muliple params in query are fix options``() =
        let query = "SELECT * FROM u WHERE u.Name = @NAME AND u.Age = @age"

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

        let msgs = CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

        msgs |> should haveLength 2

        let queryParamMsg =
            msgs
            |> List.find (fun msg -> msg.Message.Contains "@name")

        queryParamMsg.Fixes |> should haveLength 2

    [<Fact>]
    let ``Params without 'at' at start are offered a fix``() =
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

        let queryParamMsg =
            msgs
            |> List.find (fun msg -> msg.Code = Messaging.ParameterMissingSymbol.Code)

        let fix = queryParamMsg.Fixes |> Seq.tryExactlyOne

        fix |> should be (ofCase <@ Some @>)
        fix.Value.FromText |> should equal "name"
        fix.Value.ToText |> should equal "\"@name\""

    [<Fact>]
    let ``Params with 'at' at start aren't offered a fix``() =
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

        let msgs = CosmosCodeAnalyzer.analyzeParameters queryOperation parameters range.Zero

        msgs |> should haveLength 0
