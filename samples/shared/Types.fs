module Types

open FSharp.CosmosDb
open Newtonsoft.Json

type Parent =
    { FamilyName: string
      FirstName: string }

type Pet = { GivenName: string }

type Child =
    { FamilyName: string
      FirstName: string
      Gender: string
      Grade: int
      Pets: Pet array }

type Address =
    { State: string
      Country: string
      City: string }

type Family =
    { [<Id>]
      [<JsonProperty(PropertyName = "id")>]
      Id: string
      [<PartitionKey>]
      LastName: string
      IsRegistered: bool
      Parents: Parent array
      Children: Child array
      Address: Address }
