module Messaging

open FSharp.Analyzers.SDK

type AnalysisCode = { Type: string; Code: string }

let UnusedParameter =
    { Type = "Unused Parameter"
      Code = "CDB1002" }

let MissingParameter =
    { Type = "Missing Parameter"
      Code = "CDB1003" }

let ParameterMissingSymbol =
    { Type = "Parameter missing @"
      Code = "CDB2002" }

let warning msg range : Message =
    { Message = msg
      Type = "Cosmos DB Analysis"
      Code = "CDB1001"
      Severity = Warning
      Range = range
      Fixes = [] }

let error msg range : Message =
    { Message = msg
      Type = "Cosmos DB Analysis"
      Code = "CDB2001"
      Severity = Error
      Range = range
      Fixes = [] }

let info msg range : Message =
    { Message = msg
      Type = "Cosmos DB Analysis"
      Code = "CDB0001"
      Severity = Info
      Range = range
      Fixes = [] }
