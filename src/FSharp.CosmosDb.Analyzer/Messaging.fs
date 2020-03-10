module internal Messaging
open FSharp.Analyzers.SDK

let warning msg range: Message =
    { Message = msg;
      Type = "Cosmos DB Analysis";
      Code = "CDB0001";
      Severity = Warning;
      Range = range;
      Fixes = [ ] }

let error msg range: Message =
    { Message = msg;
      Type = "Cosmos DB Analysis";
      Code = "CDB0001";
      Severity = Error;
      Range = range;
      Fixes = [ ] }

let info msg range: Message =
    { Message = msg;
      Type = "Cosmos DB Analysis";
      Code = "CDB0001";
      Severity = Info;
      Range = range;
      Fixes = [ ] }
