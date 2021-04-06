namespace FSharp.CosmosDb

open System
open System.Reflection
open System.Text.Json.Serialization

[<AttributeUsageAttribute(AttributeTargets.Field
                          ||| AttributeTargets.Property,
                          AllowMultiple = false,
                          Inherited = true)>]
type IdAttribute() =
    inherit JsonAttribute()

[<RequireQualifiedAccess>]
module IdAttributeTools =
    let findIdOnType (t: Type) =
        t.GetProperties(BindingFlags.Public ||| BindingFlags.Instance)
        |> Array.filter
            (fun p ->
                let attr =
                    p.GetCustomAttribute<IdAttribute>() |> box

                not (isNull attr))
        |> Array.tryHead

    let findId<'T> () = findIdOnType typeof<'T>
