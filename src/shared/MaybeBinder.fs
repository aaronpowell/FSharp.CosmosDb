[<AutoOpen>]
module internal MaybeBinder

type MaybeBinder() =
    member __.Bind(value, binder) = Option.bind binder value

    member __.Return value = Some value

    member __.ReturnFrom value = value

    member __.Combine success fail =
        match success with
        | Some _ -> success
        | None -> fail

    member __.Delay f = f()

let maybe = MaybeBinder()
