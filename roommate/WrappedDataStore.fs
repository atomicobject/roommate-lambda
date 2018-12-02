namespace Roommate

open Google.Apis.Util.Store
open System.Threading.Tasks
open System.Xml.Schema

module WrappedDataStore =

    // let mutable x: obj option = None

    type impls = {
        get: string -> Async<obj>
        del: string -> Async<unit>
        clear: unit -> Async<unit>
        store: string -> obj -> Async<unit>
    }



    let castAsync<'T> (o:Async<obj>) : Async<'T> =
        async {
            let! oo = o
            return oo :?> 'T
        } 

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


    type InMemoryDataStore () =
        let mutable map: Map<string,obj> = [] |> Map.ofList

        let inMemory : impls = {
            get = fun (key:string) ->
                async {
                    return match map.TryFind key with
                            | None -> null //failwith "bonk"
                            | Some v -> v
                }
            del = fun key ->
                map <- map.Remove(key)
                async {return () }
            clear = fun () ->
                map <- [] |> Map.ofList
                async {return () }
            store = fun (key:string) (value:obj) ->
                map <- map.Add(key,value)
                async {return () }
        }
        member this.store = new WrappedDataStore(inMemory)
        member this.getMap () = map

    type LoggingDataStore () =
        let mutable map: Map<string,obj> = [] |> Map.ofList

        let logFns : impls = {
            get = fun (key:string) ->
                printfn "get %s" key
                async {
                    return match map.TryFind key with
                            | None -> null
                            | Some v -> v
                }
            del = fun key ->
                printfn "del %s" key
                map <- map.Remove(key)
                async {return () }
            clear = fun () ->
                printfn "clear"
                map <- [] |> Map.ofList
                async {return () }
            store = fun (key:string) (value:obj) ->
                let json = Newtonsoft.Json.JsonConvert.SerializeObject(value)
                printfn "store %s %s" key json
                map <- map.Add(key,value)
                async {return () }
        }
        member this.store = new WrappedDataStore(logFns)
        member this.getMap () = map
