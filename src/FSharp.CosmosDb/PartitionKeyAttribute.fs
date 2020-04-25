namespace FSharp.CosmosDb

open System
open System.Reflection

[<AttributeUsageAttribute(AttributeTargets.Field ||| AttributeTargets.Property, AllowMultiple = false, Inherited = true)>]
type PartitionKeyAttribute() =
    inherit Attribute()

[<RequireQualifiedAccess>]
module PartitionKeyAttributeTools =
    let findPartitionKey<'T>() =
        let t = typeof<'T>

        t.GetProperties(BindingFlags.Public ||| BindingFlags.Instance)
        |> Array.filter (fun p ->
            let attr = p.GetCustomAttribute<PartitionKeyAttribute>() |> box
            not (isNull attr))
        |> Array.tryHead
