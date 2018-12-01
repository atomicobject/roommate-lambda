namespace Roommate.Tests

open Xunit
open FsUnit

// open WrappedDataStore
module WrappedDataStoreTest =
    open System.Threading.Tasks
    open Google.Apis.Util.Store
    open Roommate.WrappedDataStore

    [<Fact>]
    let ``stores and recalls string``() =
        let subject = new WrappedDataStore(inMemory) :> IDataStore
        subject.StoreAsync("key","val") |> Async.AwaitTask |> Async.RunSynchronously
        let x = subject.GetAsync<string>("key").Result
        Assert.Equal(x,"val")

    [<Fact>]
    let ``can hold multiple things``() =
        let subject = new WrappedDataStore(inMemory) :> IDataStore
        subject.StoreAsync("k1","v1") |> Async.AwaitTask |> Async.RunSynchronously
        subject.StoreAsync("k2","v2") |> Async.AwaitTask |> Async.RunSynchronously
        let v1 = subject.GetAsync<string>("k1").Result
        let v2 = subject.GetAsync<string>("k2").Result
        Assert.Equal(v1,"v1")
        Assert.Equal(v2,"v2")
        
    [<Fact>]
    let ``can delete entry``() =
        let subject = new WrappedDataStore(inMemory) :> IDataStore
        subject.StoreAsync("key","val") |> Async.AwaitTask |> Async.RunSynchronously
        subject.DeleteAsync("key")  |> Async.AwaitTask |> Async.RunSynchronously
        (fun () -> subject.GetAsync<string>("key").Result |> ignore) |> should throw typeof<System.AggregateException>

    [<Fact>]
    let ``can clear``() =
        let subject = new WrappedDataStore(inMemory) :> IDataStore
        subject.StoreAsync("key","val") |> Async.AwaitTask |> Async.RunSynchronously
        subject.ClearAsync() |> Async.AwaitTask |> Async.RunSynchronously
        let x = subject.ClearAsync()  |> Async.AwaitTask |> Async.RunSynchronously

        (fun () -> subject.GetAsync<string>("key").Result |> ignore) |> should throw typeof<System.AggregateException>

    [<Fact>]
    let ``blows up on type mismatch``() =
        let subject = new WrappedDataStore(inMemory) :> IDataStore
        subject.StoreAsync("key",System.DateTime.Now) |> Async.AwaitTask |> Async.RunSynchronously
        (fun () -> subject.GetAsync<string>("key").Result |> ignore) |> should throw typeof<System.AggregateException>
