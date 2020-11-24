namespace FSharp.CosmosDb.Analyzer

open FSharp.Analyzers.SDK
open FSharp.Compiler.Range
open FSharp.Compiler.SyntaxTree

module CosmosCodeAnalysis =
    let dotConcat = List.map(fun (id:Ident) -> id.idText) >> String.concat "."
    let (|Apply|_|) synExpr =
        match synExpr with
        | SynExpr.TypeApp (funcExpr, _, _, _, _, _, range) -> Some (funcExpr, funcExpr, range)
        | SynExpr.App (_, _, funcExpr, argExpr, range) -> Some (funcExpr, argExpr, range)
        | _ -> None
        |> Option.bind(fun (funcExpr, argExpr, range) ->
            match funcExpr with
            | SynExpr.Ident ident ->
                Some(ident.idText, argExpr, funcExpr.Range, range)
            | SynExpr.LongIdent (_, LongIdentWithDots (listOfIds, _), _, _) ->
                Some(dotConcat listOfIds, argExpr, funcExpr.Range, range)
            | _ ->
                None)

    let (|LongIdent|_|) =
        function
        | SynExpr.LongIdent (isOptional, LongIdentWithDots (listOfIds, ranges), altName, identRange) ->
            Some(dotConcat listOfIds, range)
        | _ ->
            None

    let (|Query|_|) =
        function
        | Apply ("Cosmos.query", SynExpr.Const (SynConst.String (query, queryRange), constRange), range, appRange) -> Some(query, constRange)
        | _ -> None

    let (|LiteralQuery|_|) =
        function
        | Apply ("Cosmos.query", SynExpr.Ident (identifier), funcRange, appRange) -> Some(identifier.idText, funcRange)
        | _ -> None

    let (|TypedQuery|_|) synExpr =
        match synExpr with
        | SynExpr.App (_, _, (SynExpr.TypeApp (funcExpr, _, typeNames, _, _, _, typeAppRange)), SynExpr.Const (SynConst.String (query, _), _), _) ->
            Some {| FuncExpr = funcExpr; TypeNames = typeNames; Range = typeAppRange; Query = query |}
        | _ ->
            None
        |> Option.bind(fun args ->
            match args.FuncExpr with
            | SynExpr.LongIdent (_, LongIdentWithDots (listOfIds, _), _, _) ->
                Some {| args with Ids = listOfIds |}
            | _ ->
                None)
        |> Option.bind(fun args ->
            match dotConcat args.Ids with
            | "Cosmos.query" ->
                let names =
                    args.TypeNames
                    |> List.choose (fun typeName ->
                        match typeName with
                        | SynType.LongIdent (LongIdentWithDots (listOfIds, _)) ->
                            dotConcat listOfIds |> Some
                        | _ ->
                            None)

                Some(names, args.Query, args.Range)
            | _ ->
                None)

    let (|Database|_|) =
        function
        | Apply ("Cosmos.database", SynExpr.Const (SynConst.String (dbId, queryRange), constRange), range, appRange) -> Some(dbId, constRange)
        | _ -> None

    let (|LiteralDatabase|_|) =
        function
        | Apply ("Cosmos.database", SynExpr.Ident (identifier), funcRange, appRange) -> Some(identifier.idText, funcRange)
        | _ -> None

    let (|Container|_|) =
        function
        | Apply ("Cosmos.container", SynExpr.Const (SynConst.String (containerName, queryRange), constRange), range, appRange) ->
            Some(containerName, constRange)
        | _ -> None

    let (|LiteralContainer|_|) =
        function
        | Apply ("Cosmos.container", SynExpr.Ident (identifier), funcRange, appRange) -> Some(identifier.idText, funcRange)
        | _ -> None

    let (|ParameterTuple|_|) =
        function
        | SynExpr.Tuple (isStruct,
                         [ SynExpr.Const (SynConst.String (parameterName, paramRange), constRange); Apply (funcName, exprArgs, funcRange, appRange) ],
                         commaRange,
                         tupleRange) -> Some(parameterName, paramRange, funcName, funcRange, Some appRange)
        | SynExpr.Tuple (isStruct, [ SynExpr.Const (SynConst.String (parameterName, paramRange), constRange); secondItem ], commaRange, tupleRange) ->
            match secondItem with
            | SynExpr.LongIdent (isOptional, longDotId, altName, identRange) ->
                match longDotId with
                | LongIdentWithDots (listOfIds, ranges) ->
                    let fullName =
                        listOfIds
                        |> List.map (fun id -> id.idText)
                        |> String.concat "."

                    Some(parameterName, paramRange, fullName, identRange, None)
            | _ -> None
        | SynExpr.Tuple (isStruct, [ firstItem; secondItem ], commaRange, tupleRange) ->
            printfn "Tuple: %A %A" firstItem secondItem
            None
        | x ->
            printfn "No idea: %A" x
            None

    let rec readParameters =
        function
        | ParameterTuple (name, range, func, funcRange, appRange) -> [ name, range, func, funcRange, appRange ]
        | SynExpr.Sequential (debugSeqPoint, isTrueSeq, expr1, expr2, seqRange) ->
            [ yield! readParameters expr1
              yield! readParameters expr2 ]
        | _ -> []

    let (|Parameters|_|) =
        function
        | Apply ("Cosmos.parameters", SynExpr.ArrayOrListOfSeqExpr (isArray, listExpr, listRange), funcRange, appRange) ->
            match listExpr with
            | SynExpr.CompExpr (isArrayOfList, isNotNakedRefCell, compExpr, compRange) -> Some(readParameters compExpr, compRange)
            | _ -> None
        | _ -> None

    let rec findQuery =
        function
        | Query (query, range) -> [ CosmosAnalyzerBlock.Query(query, range) ]
        | LiteralQuery (identifier, range) -> [ CosmosAnalyzerBlock.LiteralQuery(identifier, range) ]
        | TypedQuery (typeNames, query, typeAppRange) -> [ CosmosAnalyzerBlock.Query(query, typeAppRange) ]
        | SynExpr.App (exprAtomic, isInfix, funcExpr, argExpr, range) ->
            [ yield! findQuery funcExpr
              yield! findQuery argExpr ]
        | _ -> []

    let rec findDatabase =
        function
        | Database (query, range) -> [ CosmosAnalyzerBlock.DatabaseId(query, range) ]
        | LiteralQuery (identifier, range) -> [ CosmosAnalyzerBlock.LiteralDatabaseId(identifier, range) ]
        | SynExpr.App (exprAtomic, isInfix, funcExpr, argExpr, range) ->
            [ yield! findDatabase funcExpr
              yield! findDatabase argExpr ]
        | _ -> []

    let rec findContainerName =
        function
        | Container (query, range) -> [ CosmosAnalyzerBlock.ContainerName(query, range) ]
        | LiteralQuery (identifier, range) -> [ CosmosAnalyzerBlock.LiteralContainerName(identifier, range) ]
        | SynExpr.App (exprAtomic, isInfix, funcExpr, argExpr, range) ->
            [ yield! findContainerName funcExpr
              yield! findContainerName argExpr ]
        | _ -> []

    let rec findParameters =
        function
        | Parameters (parameters, range) ->
            let queryParams =
                parameters
                |> List.map (fun (name, range, func, funcRange, appRange) ->
                    { name = name.TrimStart('@')
                      range = range
                      paramFunc = func
                      paramFuncRange = funcRange
                      applicationRange = appRange })

            [ CosmosAnalyzerBlock.Parameters(queryParams, range) ]

        | SynExpr.App (exprAtomic, isInfix, funcExpr, argExpr, range) ->
            [ yield! findParameters funcExpr
              yield! findParameters argExpr ]

        | _ -> []

    // This represents the "tail" of our AST, so we match on all the ways that it could end
    // and then walk backwards from there to find the other parts that should exist
    let rec visitSyntacticExpression (expr: SynExpr) (fullExpressionRange: range) =
        match expr with
        | SynExpr.App (exprAtomic, isInfix, funcExpr, argExpr, range) ->
            match argExpr with
            | Apply (("Cosmos.execAsync"), lambdaExp, funcRange, appRange) ->
                let blocks =
                    [ yield! findQuery funcExpr
                      yield! findDatabase funcExpr
                      yield! findContainerName funcExpr
                      yield! findParameters funcExpr ]

                [ { blocks = blocks; range = range } ]

            | LongIdent (("Cosmos.execAsync"), appRange) ->
                let blocks =
                    [ yield! findQuery funcExpr
                      yield! findDatabase funcExpr
                      yield! findContainerName funcExpr
                      yield! findParameters funcExpr ]

                [ { blocks = blocks; range = range } ]

            | Query (query, queryRange) ->
                let blocks =
                    [ CosmosAnalyzerBlock.Query(query, queryRange) ]

                [ { blocks = blocks; range = range } ]

            | LiteralQuery (identifier, queryRange) ->
                let blocks =
                    [ CosmosAnalyzerBlock.LiteralQuery(identifier, queryRange) ]

                [ { blocks = blocks; range = range } ]

            | Parameters (parameters, queryRange) ->
                let queryParams =
                    parameters
                    |> List.map (fun (name, range, func, funcRange, appRange) ->
                        { name = name.TrimStart('@')
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

            | Apply (anyOtherFunc, funcArgs, range, appRange) ->
                let blocks =
                    [ yield! findQuery funcExpr
                      yield! findDatabase funcExpr
                      yield! findContainerName funcExpr
                      yield! findParameters funcExpr ]

                [ { blocks = blocks; range = range } ]

            | _ -> []

        | SynExpr.LetOrUse (isRecursive, isUse, bindings, body, range) ->
            [ yield! visitSyntacticExpression body range
              for binding in bindings do
                  yield! visitBinding binding ]

        | _ -> []

    and visitBinding (binding: SynBinding): CosmosOperation list =
        match binding with
        | SynBinding.Binding (access, kind, mustInline, isMutable, attrs, xmlDecl, valData, headPat, returnInfo, expr, range, seqPoint) ->
            visitSyntacticExpression expr range


    let findOperations (ctx: Context) =
        let operations = ResizeArray<CosmosOperation>()
        match ctx.ParseTree with
        | ParsedInput.ImplFile input ->
            match input with
            | ParsedImplFileInput.ParsedImplFileInput (fileName, isScript, qualifiedName, _, _, modules, _) ->
                for parsedModule in modules do
                    match parsedModule with
                    | SynModuleOrNamespace (identifier, isRecursive, kind, declarations, _, _, _, _) ->
                        for declaration in declarations do
                            match declaration with
                            | SynModuleDecl.Let (isRecursiveDef, bindings, range) ->
                                for binding in bindings do
                                    operations.AddRange(visitBinding binding)
                            | _ -> ()

        | ParsedInput.SigFile file -> ()

        operations |> Seq.toList
