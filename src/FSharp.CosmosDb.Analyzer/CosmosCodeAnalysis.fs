namespace FSharp.CosmosDb.Analyzer

open FSharp.Analyzers.SDK
open FSharp.Compiler.Ast
open FSharp.Compiler.Range

module CosmosCodeAnalysis =
    let (|Apply|_|) =
        function
        | SynExpr.TypeApp(funcExpr, lessRange, typeNames, commasRange, greaterRange, typeArgsRange, range) ->
            match funcExpr with
            | SynExpr.Ident ident -> Some(ident.idText, funcExpr, funcExpr.Range, range)
            | SynExpr.LongIdent(isOptional, longDotId, altName, identRange) ->
                match longDotId with
                | LongIdentWithDots(listOfIds, ranges) ->
                    let fullName =
                        listOfIds
                        |> List.map (fun id -> id.idText)
                        |> String.concat "."

                    Some(fullName, funcExpr, funcExpr.Range, range)
            | _ -> None
        | SynExpr.App(atomicFlag, isInfix, funcExpr, argExpr, applicationRange) ->
            match funcExpr with
            | SynExpr.Ident ident -> Some(ident.idText, argExpr, funcExpr.Range, applicationRange)
            | SynExpr.LongIdent(isOptional, longDotId, altName, identRange) ->
                match longDotId with
                | LongIdentWithDots(listOfIds, ranges) ->
                    let fullName =
                        listOfIds
                        |> List.map (fun id -> id.idText)
                        |> String.concat "."

                    Some(fullName, argExpr, funcExpr.Range, applicationRange)
            | _ -> None
        | _ -> None

    let (|Query|_|) =
        function
        | Apply("Cosmos.query", SynExpr.Const(SynConst.String(query, queryRange), constRange), range, appRange) ->
            Some(query, constRange)
        | _ -> None

    let (|LiteralQuery|_|) =
        function
        | Apply("Cosmos.query", SynExpr.Ident(identifier), funcRange, appRange) -> Some(identifier.idText, funcRange)
        | _ -> None

    let (|Database|_|) =
        function
        | Apply("Cosmos.database", SynExpr.Const(SynConst.String(dbId, queryRange), constRange), range, appRange) ->
            Some(dbId, constRange)
        | _ -> None

    let (|LiteralDatabase|_|) =
        function
        | Apply("Cosmos.database", SynExpr.Ident(identifier), funcRange, appRange) -> Some(identifier.idText, funcRange)
        | _ -> None

    let (|Container|_|) =
        function
        | Apply("Cosmos.container", SynExpr.Const(SynConst.String(containerName, queryRange), constRange), range,
                appRange) -> Some(containerName, constRange)
        | _ -> None

    let (|LiteralContainer|_|) =
        function
        | Apply("Cosmos.container", SynExpr.Ident(identifier), funcRange, appRange) ->
            Some(identifier.idText, funcRange)
        | _ -> None

    let (|ParameterTuple|_|) =
        function
        | SynExpr.Tuple(isStruct,
                        [ SynExpr.Const(SynConst.String(parameterName, paramRange), constRange);
                          Apply(funcName, exprArgs, funcRange, appRange) ], commaRange, tupleRange) ->
            Some(parameterName, paramRange, funcName, funcRange, Some appRange)
        | SynExpr.Tuple(isStruct, [ SynExpr.Const(SynConst.String(parameterName, paramRange), constRange); secondItem ],
                        commaRange, tupleRange) ->
            match secondItem with
            | SynExpr.LongIdent(isOptional, longDotId, altName, identRange) ->
                match longDotId with
                | LongIdentWithDots(listOfIds, ranges) ->
                    let fullName =
                        listOfIds
                        |> List.map (fun id -> id.idText)
                        |> String.concat "."

                    Some(parameterName, paramRange, fullName, identRange, None)
            | _ -> None
        | SynExpr.Tuple(isStruct, [ firstItem; secondItem ], commaRange, tupleRange) ->
            printfn "%A %A" firstItem secondItem
            None
        | x ->
            printfn "%A" x
            None

    let rec readParameters =
        function
        | ParameterTuple(name, range, func, funcRange, appRange) -> [ name, range, func, funcRange, appRange ]
        | SynExpr.Sequential(SequencePointInfoForSeq.SequencePointsAtSeq, isTrueSeq, expr1, expr2, seqRange) ->
            [ yield! readParameters expr1
              yield! readParameters expr2 ]
        | _ -> []

    let (|Parameters|_|) =
        function
        | Apply("Cosmos.parameters", SynExpr.ArrayOrListOfSeqExpr(isArray, listExpr, listRange), funcRange, appRange) ->
            match listExpr with
            | SynExpr.CompExpr(isArrayOfList, isNotNakedRefCell, compExpr, compRange) ->
                Some(readParameters compExpr, compRange)
            | _ -> None
        | _ -> None

    let rec findQuery =
        function
        | Query(query, range) -> [ CosmosAnalyzerBlock.Query(query, range) ]
        | LiteralQuery(identifier, range) -> [ CosmosAnalyzerBlock.LiteralQuery(identifier, range) ]
        | SynExpr.App(exprAtomic, isInfix, funcExpr, argExpr, range) ->
            [ yield! findQuery funcExpr
              yield! findQuery argExpr ]
        | _ -> []

    let rec findDatabase =
        function
        | Database(query, range) -> [ CosmosAnalyzerBlock.DatabaseId(query, range) ]
        | LiteralQuery(identifier, range) -> [ CosmosAnalyzerBlock.LiteralDatabaseId(identifier, range) ]
        | SynExpr.App(exprAtomic, isInfix, funcExpr, argExpr, range) ->
            [ yield! findDatabase funcExpr
              yield! findDatabase argExpr ]
        | _ -> []

    let rec findContainerName =
        function
        | Container(query, range) -> [ CosmosAnalyzerBlock.ContainerName(query, range) ]
        | LiteralQuery(identifier, range) -> [ CosmosAnalyzerBlock.LiteralContainerName(identifier, range) ]
        | SynExpr.App(exprAtomic, isInfix, funcExpr, argExpr, range) ->
            [ yield! findContainerName funcExpr
              yield! findContainerName argExpr ]
        | _ -> []

    let rec findParameters =
        function
        | Parameters(parameters, range) ->
            let queryParams =
                parameters
                |> List.map (fun (name, range, func, funcRange, appRange) ->
                    { name = name.TrimStart('@')
                      range = range
                      paramFunc = func
                      paramFuncRange = funcRange
                      applicationRange = appRange })
            [ CosmosAnalyzerBlock.Parameters(queryParams, range) ]

        | SynExpr.App(exprAtomic, isInfix, funcExpr, argExpr, range) ->
            [ yield! findParameters funcExpr
              yield! findParameters argExpr ]

        | _ -> []

    let rec visitSyntacticExpression (expr: SynExpr) (fullExpressionRange: range) =
        match expr with
        | SynExpr.App(exprAtomic, isInfix, funcExpr, argExpr, range) ->
            match argExpr with
            | Apply(("Cosmos.execAsync"), lambdaExp, funcRange, appRange) ->
                let blocks =
                    [ yield! findQuery funcExpr
                      yield! findDatabase funcExpr
                      yield! findContainerName funcExpr
                      yield! findParameters funcExpr ]

                [ { blocks = blocks
                    range = range } ]

            | Query(query, queryRange) ->
                let blocks = [ CosmosAnalyzerBlock.Query(query, queryRange) ]

                [ { blocks = blocks
                    range = range } ]

            | LiteralQuery(identifier, queryRange) ->
                let blocks = [ CosmosAnalyzerBlock.LiteralQuery(identifier, queryRange) ]

                [ { blocks = blocks
                    range = range } ]

            | Parameters(parameters, queryRange) ->
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

                [ { blocks = blocks
                    range = range } ]

            | Apply(anyOtherFunc, funcArgs, range, appRange) ->
                let blocks =
                    [ yield! findQuery funcExpr
                      yield! findDatabase funcExpr
                      yield! findContainerName funcExpr
                      yield! findParameters funcExpr ]

                [ { blocks = blocks
                    range = range } ]

            | _ -> []

        | SynExpr.LetOrUse(isRecursive, isUse, bindings, body, range) ->
            [ yield! visitSyntacticExpression body range
              for binding in bindings do
                  yield! visitBinding binding ]

        | _ -> []

    and visitBinding (binding: SynBinding): CosmosOperation list =
        match binding with
        | SynBinding.Binding(access, kind, mustInline, isMutable, attrs, xmlDecl, valData, headPat, returnInfo, expr,
                             range, seqPoint) -> visitSyntacticExpression expr range


    let findOperations (ctx: Context) =
        let operations = ResizeArray<CosmosOperation>()
        match ctx.ParseTree with
        | ParsedInput.ImplFile input ->
            match input with
            | ParsedImplFileInput.ParsedImplFileInput(fileName, isScript, qualifiedName, _, _, modules, _) ->
                for parsedModule in modules do
                    match parsedModule with
                    | SynModuleOrNamespace(identifier, isRecursive, kind, declarations, _, _, _, _) ->
                        for declaration in declarations do
                            match declaration with
                            | SynModuleDecl.Let(isRecursiveDef, bindings, range) ->
                                for binding in bindings do
                                    operations.AddRange(visitBinding binding)
                            | _ -> ()

        | ParsedInput.SigFile file -> ()

        operations |> Seq.toList
