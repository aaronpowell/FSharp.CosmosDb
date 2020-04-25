[<AutoOpen>]
module MaybeBinder

type MaybeBinder() =
    member __.Bind(value, binder) = Option.bind binder value

    member __.Return value = Some value

let maybe = MaybeBinder()
