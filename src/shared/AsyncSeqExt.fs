[<AutoOpen>]
module FSharp.Control.AsyncSeq

open System
open Microsoft.Azure.Cosmos

let ofAsyncFeedIterator (source: FeedIterator<_>) =
    asyncSeq {
        while (source.HasMoreResults) do
            let! response = source.ReadNextAsync() |> Async.AwaitTask
            yield! response |> AsyncSeq.ofSeq
    }
