namespace FSharp.CosmosDb.Analyzer

open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open FSharp.Compiler.Syntax

module CosmosCodeAnalysis =

    let dotConcat =
        List.map (fun (id: Ident) -> id.idText)
        >> String.concat "."

    let checkIfApply funcExpr argExpr range =
        match funcExpr with
        | SynExpr.Ident ident -> Some(ident.idText, argExpr, funcExpr.Range, range)
        | SynExpr.LongIdent (_, SynLongIdent (listOfIds, _, _), _, _) ->
            Some(dotConcat listOfIds, argExpr, funcExpr.Range, range)
        | _ -> None

    let (|Apply|_|) synExpr =
        match synExpr with
        | SynExpr.TypeApp (funcExpr, _, _, _, _, _, range) -> checkIfApply funcExpr funcExpr range
        | SynExpr.App (_, _, funcExpr, argExpr, range) -> checkIfApply funcExpr argExpr range
        | _ -> None

    let (|LongIdent|_|) =
        function
        | SynExpr.LongIdent (_, SynLongIdent (listOfIds, _, _), _, _) -> Some(dotConcat listOfIds, range)
        | _ -> None

    // for match of:
    // Cosmos.query "..." ...
    let (|Query|_|) =
        function
        | Apply ("Cosmos.query", SynExpr.Const (SynConst.String (query, _, _), constRange), _, _) ->
            Some(query, constRange)
        | _ -> None

    // for match of:
    // let query = "..."
    // Cosmos.query query ...
    let (|LiteralQuery|_|) =
        function
        | Apply ("Cosmos.query", SynExpr.Ident identifier, funcRange, _) -> Some(identifier.idText, funcRange)
        | _ -> None

    // for match of:
    // Cosmos.query<Foo> "..." ...
    let (|TypedQuery|_|) synExpr =
        match synExpr with
        | SynExpr.App (_,
                       _,
                       (SynExpr.TypeApp (SynExpr.LongIdent (_, SynLongIdent (listOfIds, _, _), _, _),
                                         _,
                                         typeNames,
                                         _,
                                         _,
                                         _,
                                         _)),
                       SynExpr.Const (SynConst.String (query, _, queryRange), _),
                       _) ->
            match dotConcat listOfIds with
            | "Cosmos.query" ->
                let names =
                    typeNames
                    |> List.choose (fun typeName ->
                        match typeName with
                        | SynType.LongIdent (SynLongIdent (listOfIds, _, _)) -> dotConcat listOfIds |> Some
                        | _ -> None)

                Some(names, query, queryRange)
            | _ -> None
        | _ -> None

    let (|Database|_|) =
        function
        | Apply ("Cosmos.database", SynExpr.Const (SynConst.String (dbId, _, _), constRange), _, _) ->
            Some(dbId, constRange)
        | _ -> None

    let (|LiteralDatabase|_|) =
        function
        | Apply ("Cosmos.database", SynExpr.Ident identifier, funcRange, _) -> Some(identifier.idText, funcRange)
        | _ -> None

    let (|Container|_|) =
        function
        | Apply ("Cosmos.container", SynExpr.Const (SynConst.String (containerName, _, _), constRange), _, _) ->
            Some(containerName, constRange)
        | _ -> None

    let (|LiteralContainer|_|) =
        function
        | Apply ("Cosmos.container", SynExpr.Ident identifier, funcRange, _) -> Some(identifier.idText, funcRange)
        | _ -> None

    let (|ParameterTuple|_|) =
        function
        | SynExpr.Tuple (_,
                         [ SynExpr.Const (SynConst.String (parameterName, _, paramRange), _)
                           Apply (funcName, _, funcRange, appRange) ],
                         _,
                         _) -> Some(parameterName, paramRange, funcName, funcRange, Some appRange)
        | SynExpr.Tuple (_,
                         [ SynExpr.Const (SynConst.String (parameterName, _, paramRange), _); secondItem ],
                         _,
                         _) ->
            match secondItem with
            | SynExpr.LongIdent (_, longDotId, _, identRange) ->
                match longDotId with
                | SynLongIdent (listOfIds, _, _) ->
                    let fullName =
                        listOfIds
                        |> List.map (fun id -> id.idText)
                        |> String.concat "."

                    Some(parameterName, paramRange, fullName, identRange, None)
            | _ -> None
        | SynExpr.Tuple (_, [ firstItem; secondItem ], _, _) ->
            printfn $"Tuple: %A{firstItem} %A{secondItem}"
            None
        | x ->
            printfn $"No idea: %A{x}"
            None

    let rec readParameters =
        function
        | ParameterTuple (name, range, func, funcRange, appRange) -> [ name, range, func, funcRange, appRange ]
        | SynExpr.Sequential (_, _, expr1, expr2, _) ->
            [ yield! readParameters expr1
              yield! readParameters expr2 ]
        | _ -> []

    let (|Parameters|_|) =
        function
        | Apply ("Cosmos.parameters", SynExpr.ArrayOrListComputed (_, listExpr, _), _, _) ->
            match listExpr with
            | ParameterTuple (name, range, func, funcRange, appRange) ->
                Some([ name, range, func, funcRange, appRange ], funcRange)
            | SynExpr.ComputationExpr (_, compExpr, compRange) -> Some(readParameters compExpr, compRange)
            | _ -> None
        | _ -> None

    let rec findQuery =
        function
        | Query (query, range) -> [ CosmosAnalyzerBlock.Query(query, range) ]
        | LiteralQuery (identifier, range) -> [ CosmosAnalyzerBlock.LiteralQuery(identifier, range) ]
        | TypedQuery (_, query, queryRange) -> [ CosmosAnalyzerBlock.Query(query, queryRange) ]
        | SynExpr.App (_, _, funcExpr, argExpr, _) ->
            [ yield! findQuery funcExpr
              yield! findQuery argExpr ]
        | _ -> []

    let rec findDatabase =
        function
        | Database (query, range) -> [ CosmosAnalyzerBlock.DatabaseId(query, range) ]
        | LiteralQuery (identifier, range) -> [ CosmosAnalyzerBlock.LiteralDatabaseId(identifier, range) ]
        | SynExpr.App (_, _, funcExpr, argExpr, _) ->
            [ yield! findDatabase funcExpr
              yield! findDatabase argExpr ]
        | _ -> []

    let rec findContainerName =
        function
        | Container (query, range) -> [ CosmosAnalyzerBlock.ContainerName(query, range) ]
        | LiteralQuery (identifier, range) -> [ CosmosAnalyzerBlock.LiteralContainerName(identifier, range) ]
        | SynExpr.App (_, _, funcExpr, argExpr, _) ->
            [ yield! findContainerName funcExpr
              yield! findContainerName argExpr ]
        | _ -> []

    let rec findParameters =
        function
        | Parameters (parameters, range) ->
            let queryParams =
                parameters
                |> List.map (fun (name, range, func, funcRange, appRange) ->
                    { name = name
                      range = range
                      paramFunc = func
                      paramFuncRange = funcRange
                      applicationRange = appRange })

            [ CosmosAnalyzerBlock.Parameters(queryParams, range) ]

        | SynExpr.App (_, _, funcExpr, argExpr, _) ->
            [ yield! findParameters funcExpr
              yield! findParameters argExpr ]

        | _ -> []

    // This represents the "tail" of our AST, so we match on all the ways that it could end
    // and then walk backwards from there to find the other parts that should exist
    let rec visitSyntacticExpression (expr: SynExpr) =
        match expr with
        | SynExpr.App (_, _, funcExpr, argExpr, range) ->
            match argExpr with
            | Apply ("Cosmos.execAsync", _, _, _) ->
                let blocks =
                    [ yield! findQuery funcExpr
                      yield! findDatabase funcExpr
                      yield! findContainerName funcExpr
                      yield! findParameters funcExpr ]

                [ { blocks = blocks; range = range } ]

            | LongIdent ("Cosmos.execAsync", _) ->
                let blocks =
                    [ yield! findQuery funcExpr
                      yield! findDatabase funcExpr
                      yield! findContainerName funcExpr
                      yield! findParameters funcExpr ]

                [ { blocks = blocks; range = range } ]

            | Query (query, queryRange) ->
                let blocks = [ CosmosAnalyzerBlock.Query(query, queryRange) ]

                [ { blocks = blocks; range = range } ]

            | LiteralQuery (identifier, queryRange) ->
                let blocks = [ CosmosAnalyzerBlock.LiteralQuery(identifier, queryRange) ]

                [ { blocks = blocks; range = range } ]

            | Parameters (parameters, _) ->
                let queryParams =
                    parameters
                    |> List.map (fun (name, range, func, funcRange, appRange) ->
                        { name = name
                          range = range
                          paramFunc = func
                          paramFuncRange = funcRange
                          applicationRange = appRange })

                let blocks =
                    [ yield! findQuery funcExpr
                      yield! findDatabase funcExpr
                      yield! findContainerName funcExpr
                      yield CosmosAnalyzerBlock.Parameters(queryParams, range) ]

                [ { blocks = blocks; range = range } ]

            | Container (containerName, range) ->
                let blocks =
                    [ CosmosAnalyzerBlock.ContainerName(containerName, range)
                      yield! findDatabase funcExpr ]

                [ { blocks = blocks; range = range } ]

            | Database (dbId, range) ->
                let blocks =
                    [ CosmosAnalyzerBlock.DatabaseId(dbId, range)
                      yield! findContainerName funcExpr ]

                [ { blocks = blocks; range = range } ]

            | Apply (_, _, range, _) ->
                let blocks =
                    [ yield! findQuery funcExpr
                      yield! findDatabase funcExpr
                      yield! findContainerName funcExpr
                      yield! findParameters funcExpr ]

                [ { blocks = blocks; range = range } ]

            | _ -> []

        | SynExpr.LetOrUse (_, _, bindings, body, _, _) ->
            [ yield! visitSyntacticExpression body
              for binding in bindings do
                  yield! visitBinding binding ]

        | _ -> []

    and visitBinding (binding: SynBinding) : CosmosOperation list =
        match binding with
        | SynBinding (_, _, _, _, _, _, _, _, _, expr, _, _, _) -> visitSyntacticExpression expr


    let findOperations (ctx: EditorContext) =
        let operations = ResizeArray<CosmosOperation>()

        match ctx.ParseFileResults.ParseTree with
        | ParsedInput.ImplFile input ->
            match input with
            | ParsedImplFileInput (_, _, _, _, _, modules, _, _, _) ->
                for parsedModule in modules do
                    match parsedModule with
                    | SynModuleOrNamespace (_, _, _, declarations, _, _, _, _, _) ->
                        for declaration in declarations do
                            match declaration with
                            | SynModuleDecl.Let (_, bindings, _) ->
                                for binding in bindings do
                                    operations.AddRange(visitBinding binding)
                            | _ -> ()

        | ParsedInput.SigFile _ -> ()

        operations |> Seq.toList
