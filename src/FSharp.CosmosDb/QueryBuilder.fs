namespace FSharp.CosmosDb

open System
open System.Linq
open FSharp.Linq
open FSharp.Quotations
open FSharp.Quotations.DerivedPatterns
open FSharp.Quotations.ExprShape

type CosmosQueryBuilder() =
    inherit QueryBuilder()

    static let rec replace =
        // Replace F# functions with BCL equivalents.
        function
        | SpecificCall <@@ abs @@> (_, [ typ ], args) ->
            let e = replace args.Head
            if typ = typeof<int8> then
                <@@ (Math.Abs : int8 -> int8) %%e @@>
            elif typ = typeof<int16> then
                <@@ (Math.Abs : int16 -> int16) %%e @@>
            elif typ = typeof<int32> then
                <@@ (Math.Abs : int32 -> int32) %%e @@>
            elif typ = typeof<int64> then
                <@@ (Math.Abs : int64 -> int64) %%e @@>
            elif typ = typeof<float32> then
                <@@ (Math.Abs : float32 -> float32) %%e @@>
            elif typ = typeof<float> then
                <@@ (Math.Abs : float -> float) %%e @@>
            elif typ = typeof<decimal> then
                <@@ (Math.Abs : decimal -> decimal) %%e @@>
            else 
                failwith $"Invalid argument type for translation of 'abs': {typ.FullName}"
        | SpecificCall <@@ acos @@> (_, [ _ ], args) ->
            let e = replace args.Head
            <@@ (Math.Acos : float -> float) %%e @@>
        | ShapeVar v -> Expr.Var v
        | ShapeLambda(v, expr) -> Expr.Lambda(v, replace expr)
        | ShapeCombination(o, args) -> 
            RebuildShapeCombination(o, List.map replace args)

    member _.Run(e: Expr<QuerySource<'a, IQueryable>>) =
        let r = Expr.Cast<QuerySource<'a, IQueryable>>(replace e)
        base.Run r

[<AutoOpen>]
module QueryBuilder =
    let cosmosQuery = CosmosQueryBuilder()