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
        | SpecificCall <@@ abs @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<int8> then
                <@@ (Math.Abs: int8 -> int8) %%e @@>
            elif typ = typeof<int16> then
                <@@ (Math.Abs: int16 -> int16) %%e @@>
            elif typ = typeof<int32> then
                <@@ (Math.Abs: int32 -> int32) %%e @@>
            elif typ = typeof<int64> then
                <@@ (Math.Abs: int64 -> int64) %%e @@>
            elif typ = typeof<float32> then
                <@@ (Math.Abs: float32 -> float32) %%e @@>
            elif typ = typeof<float> then
                <@@ (Math.Abs: float -> float) %%e @@>
            elif typ = typeof<decimal> then
                <@@ (Math.Abs: decimal -> decimal) %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof abs}': {typ.FullName}"

        | SpecificCall <@@ acos @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<float32> then
                <@@ Math.Acos(float %%e) @@>
            elif typ = typeof<float> then
                <@@ Math.Acos %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof acos}': {typ.FullName}"

        | SpecificCall <@@ asin @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<float32> then
                <@@ Math.Asin(float %%e) @@>
            elif typ = typeof<float> then
                <@@ Math.Asin %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof asin}': {typ.FullName}"

        | SpecificCall <@@ atan @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<float32> then
                <@@ Math.Atan(float %%e) @@>
            elif typ = typeof<float> then
                <@@ Math.Atan %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof atan}': {typ.FullName}"

        | SpecificCall <@@ ceil @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<float32> then
                <@@ Math.Ceiling(float %%e) @@>
            elif typ = typeof<float> then
                <@@ (Math.Ceiling: float -> float) %%e @@>
            elif typ = typeof<decimal> then
                <@@ (Math.Ceiling: float -> float) %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof ceil}': {typ.FullName}"

        | SpecificCall <@@ cos @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<float32> then
                <@@ Math.Cos(float %%e) @@>
            elif typ = typeof<float> then
                <@@ Math.Cos %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof cos}': {typ.FullName}"

        | SpecificCall <@@ exp @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<float32> then
                <@@ Math.Exp(float %%e) @@>
            elif typ = typeof<float> then
                <@@ Math.Exp %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof exp}': {typ.FullName}"

        | SpecificCall <@@ floor @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<float32> then
                <@@ Math.Floor(float %%e) @@>
            elif typ = typeof<float> then
                <@@ (Math.Floor: float -> float) %%e @@>
            elif typ = typeof<decimal> then
                <@@ (Math.Floor: decimal -> decimal) %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof floor}': {typ.FullName}"

        | SpecificCall <@@ log @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<float32> then
                <@@ Math.Log(float %%e) @@>
            elif typ = typeof<float> then
                <@@ Math.Log %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof log}': {typ.FullName}"

        | SpecificCall <@@ log10 @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<float32> then
                <@@ Math.Log10(float %%e) @@>
            elif typ = typeof<float> then
                <@@ Math.Log10 %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof log10}': {typ.FullName}"

        | SpecificCall <@@ ( ** ) @@> (_, [ typ; _ ], [ x; n ])
        | SpecificCall <@@ pown @@> (_, [ typ; _ ], [ x; n ]) ->
            let x = replace x
            let n = replace n

            if typ = typeof<float> then
                <@@ Math.Pow(%%x, float %%n) @@>
            else
                <@@ Math.Pow(float %%x, float %%n) @@>

        | SpecificCall <@@ round @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<float32> then
                <@@ Math.Round(float %%e) @@>
            elif typ = typeof<float> then
                <@@ (Math.Round: float -> float) %%e @@>
            elif typ = typeof<decimal> then
                <@@ (Math.Round: decimal -> decimal) %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof round}': {typ.FullName}"

        | SpecificCall <@@ sign @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<int8> then
                <@@ (Math.Sign: int8 -> int) %%e @@>
            elif typ = typeof<int16> then
                <@@ (Math.Sign: int16 -> int) %%e @@>
            elif typ = typeof<int> then
                <@@ (Math.Sign: int -> int) %%e @@>
            elif typ = typeof<int64> then
                <@@ (Math.Sign: int64 -> int) %%e @@>
            elif typ = typeof<float32> then
                <@@ (Math.Sign: float32 -> int) %%e @@>
            elif typ = typeof<float> then
                <@@ (Math.Sign: float -> int) %%e @@>
            elif typ = typeof<decimal> then
                <@@ (Math.Sign: decimal -> int) %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof sign}': {typ.FullName}"

        | SpecificCall <@@ sin @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<float32> then
                <@@ Math.Sin(float %%e) @@>
            elif typ = typeof<float> then
                <@@ Math.Sin %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof sin}': {typ.FullName}"

        | SpecificCall <@@ sqrt @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<float> then
                <@@ Math.Sqrt %%e @@>
            else
                <@@ Math.Sqrt(float %%e) @@>

        | SpecificCall <@@ tan @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<float32> then
                <@@ Math.Tan(float %%e) @@>
            elif typ = typeof<float> then
                <@@ Math.Tan %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof tan}': {typ.FullName}"

        | SpecificCall <@@ truncate @@> (_, [ typ ], [ arg ]) ->
            let e = replace arg

            if typ = typeof<float32> then
                <@@ Math.Truncate(float %%e) @@>
            elif typ = typeof<float> then
                <@@ (Math.Truncate: float -> float) %%e @@>
            elif typ = typeof<decimal> then
                <@@ (Math.Truncate: decimal -> decimal) %%e @@>
            else
                failwith $"Invalid argument type for translation of '{nameof truncate}': {typ.FullName}"

        | ShapeVar v -> Expr.Var v
        | ShapeLambda (v, expr) -> Expr.Lambda(v, replace expr)
        | ShapeCombination (o, args) -> RebuildShapeCombination(o, List.map replace args)

    member _.Run(e: Expr<QuerySource<'a, IQueryable>>) =
        let r = Expr.Cast<QuerySource<'a, IQueryable>>(replace e)
        base.Run r

[<AutoOpen>]
module QueryBuilder =
    let cosmosQuery = CosmosQueryBuilder()
