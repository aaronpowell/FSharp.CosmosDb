[<AutoOpen>]
module FSharp.Control.AsyncSeq

open System

// Hack because the project has to be `netstandard2.0` and this method was added to AsyncSeq in `netstandard2.1`
let ofAsyncEnum (source: Collections.Generic.IAsyncEnumerable<_>) =
    asyncSeq {
        let! ct = Async.CancellationToken
        let e = source.GetAsyncEnumerator(ct)

        use _ =
            { new IDisposable with
                member __.Dispose() =
                    e.DisposeAsync().AsTask()
                    |> Async.AwaitTask
                    |> Async.RunSynchronously }

        let mutable currentResult = true
        while currentResult do
            let! r = e.MoveNextAsync().AsTask() |> Async.AwaitTask
            currentResult <- r
            if r then yield e.Current
    }
