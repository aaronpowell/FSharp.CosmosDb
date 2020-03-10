namespace FSharp.CosmosDb.Analyzer

open FSharp.Compiler.Range

type UsedParameter =
    { name: string
      range: range
      paramFunc: string
      paramFuncRange: range
      applicationRange: range option }

[<RequireQualifiedAccess>]
type CosmosAnalyzerBlock =
    | DatabaseId of string * range
    | LiteralDatabaseId of ident: string * range
    | ContainerName of string * range
    | LiteralContainerName of ident: string * range
    | Query of string * range
    | LiteralQuery of ident: string * range
    | Parameters of UsedParameter list * range

type CosmosOperation =
    { blocks: CosmosAnalyzerBlock list
      range: range }
