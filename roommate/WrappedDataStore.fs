namespace Roommate

open Google.Apis.Util.Store
open System.Threading.Tasks

module WrappedDataStore =
    type WrappedDataStore() =
        let mutable x: obj option = None
        do printfn "yep"
        interface IDataStore with

            member this.StoreAsync((key: string),(value: 'T)): System.Threading.Tasks.Task =
                x <- Some(value :> obj)
                Task.CompletedTask

            member this.DeleteAsync(key: string) = Task.CompletedTask

            member this.GetAsync(key: string) =
                match x with
                | None -> failwith "bonk"
                | Some v -> Task.FromResult(v :?> 'T)

            member this.ClearAsync() = Task.CompletedTask
