namespace Roommate

open Google.Apis.Util.Store
open System.Threading.Tasks
open System.Xml.Schema

module WrappedDataStore =

    // let mutable x: obj option = None
    let mutable x: Map<string,obj> = [] |> Map.ofList

    type impls = {
        get: string -> Async<obj>
        del: string -> Async<unit>
        clear: unit -> Async<unit>
        store: string -> obj -> Async<unit>
    }

    let inMemory : impls = {
        get = fun (key:string) ->
            async {
                return match x.TryFind key with
                        | None -> failwith "bonk"
                        | Some v -> v
            }
        del = fun key ->
            x <- x.Remove(key)
            async {return () }
        clear = fun () ->
            x <- [] |> Map.ofList
            async {return () }
        store = fun (key:string) (value:obj) ->
            x <- x.Add(key,value)
            async {return () }
    }
    let castAsync<'T> (o:Async<obj>) : Async<'T> =
        async {
            let! oo = o
            return oo :?> 'T
        } 
        // failwith ""

    // https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/generics/constraints
    type WrappedDataStore(impls:impls) =

        let _impls = impls

        interface IDataStore with
            member __.StoreAsync((key: string),(value: 'T)): System.Threading.Tasks.Task =
                _impls.store key (value :> obj)  |> Async.StartAsTask :> Task

            member __.DeleteAsync(key: string) =
                _impls.del key |> Async.StartAsTask :> Task

            member __.GetAsync(key: string) : Task<'T> =
                let a = _impls.get key
                a |> castAsync<'T> |> Async.StartAsTask

            member __.ClearAsync() =
                _impls.clear () |> Async.StartAsTask :> Task
