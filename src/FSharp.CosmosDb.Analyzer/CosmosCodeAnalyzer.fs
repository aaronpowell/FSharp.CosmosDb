namespace FSharp.CosmosDb.Analyzer

open FSharp.Compiler.Text
open Microsoft.Azure.Cosmos
open FSharp.Control
open FSharp.Analyzers.SDK
open System.Net.Http
open System
open System.Text.RegularExpressions
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Text.Position

type ConnectionResult =
    | Error of string
    | Success of CosmosClient

module CosmosCodeAnalyzer =
    let testConnection connStr =
        let client =
            new CosmosClient(connStr, CosmosClientOptions())

        try
            client.ReadAccountAsync()
            |> Async.AwaitTask
            |> Async.RunSynchronously
            |> ignore

            Success client
        with
        | :? AggregateException as ex when
            ex.InnerExceptions
            |> Seq.exists (fun e -> e :? HttpRequestException)
            ->
            Error "Could not establish Cosmos DB connection."
        | ex ->
            printfn "%A" ex
            Error "Something unknown happened when trying to access Cosmos DB"

    let findDatabaseOperation (operation: CosmosOperation) =
        operation.blocks
        |> List.tryFind
            (function
            | CosmosAnalyzerBlock.DatabaseId (_) -> true
            | _ -> false)
        |> Option.map
            (function
            | CosmosAnalyzerBlock.DatabaseId (databaseId, range) -> (databaseId, range)
            | _ -> failwith "No database operation")

    let analyzeDatabaseOperation databaseId (range: range) (cosmosClient: CosmosClient) =
        async {
            let! result =
                cosmosClient.GetDatabaseQueryIterator<DatabaseProperties>()
                |> AsyncSeq.ofAsyncFeedIterator
                |> AsyncSeq.toListAsync

            let matching =
                result
                |> List.exists (fun db -> db.Id = databaseId)

            return
                if matching then
                    []
                else
                    let msg =
                        Messaging.warning (sprintf "The database '%s' was not found." databaseId) range

                    let fixes =
                        result
                        |> List.map
                            (fun prop ->
                                { FromRange = range
                                  FromText = databaseId
                                  ToText = sprintf "\"%s\"" prop.Id })

                    [ { msg with Fixes = fixes } ]
        }
        |> Async.RunSynchronously

    let findContainerOperation (operation: CosmosOperation) =
        operation.blocks
        |> List.tryFind
            (function
            | CosmosAnalyzerBlock.ContainerName (_) -> true
            | _ -> false)
        |> Option.map
            (function
            | CosmosAnalyzerBlock.ContainerName (containerName, range) -> (containerName, range)
            | _ -> failwith "No container name operation")

    let analyzeContainerNameOperation databaseId containerName (range: range) (cosmosClient: CosmosClient) =
        async {
            try
                let! result =
                    cosmosClient
                        .GetDatabase(databaseId)
                        .GetContainerQueryIterator<ContainerProperties>()
                    |> AsyncSeq.ofAsyncFeedIterator
                    |> AsyncSeq.toListAsync

                let matching =
                    result
                    |> List.exists (fun containerProps -> containerProps.Id = containerName)

                return
                    if matching then
                        []
                    else
                        let msg =
                            Messaging.warning (sprintf "The container name '%s' was not found." containerName) range

                        let fixes =
                            result
                            |> List.map
                                (fun prop ->
                                    { FromRange = range
                                      FromText = containerName
                                      ToText = sprintf "\"%s\"" prop.Id })

                        [ { msg with Fixes = fixes } ]
            with
            | :? AggregateException as ex when
                ex.InnerExceptions
                |> Seq.exists (fun e -> e :? CosmosException)
                ->
                return
                    [ Messaging.warning "Failed to retrieve container names, database name is probably invalid." range ]
            | ex ->
                printfn "%O" ex
                return [ Messaging.error "Fatal error talking to Cosmos DB" range ]
        }
        |> Async.RunSynchronously

    let findParameters (operation: CosmosOperation) =
        operation.blocks
        |> List.tryFind
            (function
            | CosmosAnalyzerBlock.Parameters (_) -> true
            | _ -> false)
        |> Option.map
            (function
            | CosmosAnalyzerBlock.Parameters (parameters, range) -> (parameters, range)
            | _ -> failwith "No parameter operation")

    let findQuery (operation: CosmosOperation) =
        operation.blocks
        |> List.tryFind
            (function
            | CosmosAnalyzerBlock.Query (_) -> true
            | _ -> false)
        |> Option.map
            (function
            | CosmosAnalyzerBlock.Query (query, range) -> (query, range)
            | _ -> failwith "No query operation")

    let analyzeParameters operation (suppliedParameters: UsedParameter list) (parametersRange: range) =
        match findQuery operation with
        | Some (query, queryRange) ->
            let cleanParamName (name: string) = name.Replace("\"", "")

            let suppliedParameterNames =
                suppliedParameters
                |> List.map (fun p -> cleanParamName p.name)

            let paramsInQuery =
                Regex.Matches(query, "@(\\w+)")
                |> Seq.cast<Match>
                |> Seq.map
                    (fun m ->
                        m.Groups
                        |> Seq.cast<Group>
                        |> Seq.skip 1
                        |> Seq.map (fun g -> sprintf "@%s" g.Value))
                |> Seq.collect id
                |> Seq.distinct
                |> Set.ofSeq

            let suppliedButNotUsed =
                paramsInQuery
                |> Set.difference (suppliedParameterNames |> Set.ofList)
                |> Set.toList

            let usedByNotSupplied =
                suppliedParameterNames
                |> Set.ofList
                |> Set.difference paramsInQuery
                |> Set.toList

            let excessiveParams =
                suppliedButNotUsed
                |> List.map
                    (fun p ->
                        let up =
                            suppliedParameters
                            |> List.find (fun upp -> cleanParamName upp.name = p)

                        let msg =
                            Messaging.warning
                                (sprintf "The parameter '%s' is defined but not used in the query" p)
                                up.range

                        { msg with
                              Type = Messaging.UnusedParameter.Type
                              Code = Messaging.UnusedParameter.Code
                              Fixes =
                                  paramsInQuery
                                  |> Set.toList
                                  |> List.map
                                      (fun piq ->
                                          { FromRange = up.range
                                            FromText = p
                                            ToText = sprintf "\"%s\"" piq }) })

            let missingParams =
                usedByNotSupplied
                |> List.map
                    (fun p ->
                        let paramPosInQuery = query.IndexOf p

                        let paramRangeInQuery =
                            mkRange
                                p
                                (mkPos queryRange.StartLine (queryRange.StartColumn + paramPosInQuery + 1))
                                (mkPos
                                    queryRange.EndLine
                                    (queryRange.StartColumn
                                     + paramPosInQuery
                                     + p.Length
                                     + 1))

                        let msg =
                            Messaging.warning
                                (sprintf "The parameter '%s' is defined but not provided" p)
                                paramRangeInQuery

                        { msg with
                              Type = Messaging.MissingParameter.Type
                              Code = Messaging.MissingParameter.Code
                              Fixes =
                                  suppliedParameters
                                  |> List.map
                                      (fun up ->
                                          { FromRange = paramRangeInQuery
                                            FromText = p
                                            ToText = cleanParamName up.name }) })

            let suppliedWithoutSymbol =
                suppliedParameters
                |> List.filter (fun p -> not (p.name.StartsWith("@")))
                |> List.map
                    (fun param ->
                        let msg =
                            Messaging.error (sprintf "The parameter '%s' is missing an '@'" param.name) param.range

                        { msg with
                              Code = Messaging.ParameterMissingSymbol.Code
                              Type = Messaging.ParameterMissingSymbol.Type
                              Fixes =
                                  [ { FromRange = param.range
                                      FromText = param.name
                                      ToText = cleanParamName param.name |> sprintf "\"@%s\"" } ] })

            [ yield! suppliedWithoutSymbol
              yield! missingParams
              yield! excessiveParams ]

        | None ->
            suppliedParameters
            |> List.map
                (fun p ->
                    Messaging.warning (sprintf "The parameter '%s' is defined but not used in the query" p.name) p.range)
