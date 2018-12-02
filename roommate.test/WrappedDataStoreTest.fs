namespace Roommate.Tests

open Xunit
open FsUnit
open Roommate
open Google.Apis.Util.Store

// open WrappedDataStore
module WrappedDataStoreTest =
    open System.Threading.Tasks
    open Google.Apis.Util.Store
    open Roommate.WrappedDataStore


    
    let inMemoryStore = new InMemoryDataStore ()
    let subject = inMemoryStore.store :> IDataStore

    [<Fact>]
    let ``stores and recalls string``() =
        subject.StoreAsync("key","val") |> Async.AwaitTask |> Async.RunSynchronously
        let x = subject.GetAsync<string>("key").Result
        Assert.Equal(x,"val")

    [<Fact>]
    let ``can hold multiple things``() =
        subject.StoreAsync("k1","v1") |> Async.AwaitTask |> Async.RunSynchronously
        subject.StoreAsync("k2","v2") |> Async.AwaitTask |> Async.RunSynchronously
        let v1 = subject.GetAsync<string>("k1").Result
        let v2 = subject.GetAsync<string>("k2").Result
        Assert.Equal(inMemoryStore.getMap().Count,2)
        Assert.Equal(v1,"v1")
        Assert.Equal(v2,"v2")
        
    [<Fact>]
    let ``can delete entry``() =
        subject.StoreAsync("key","val") |> Async.AwaitTask |> Async.RunSynchronously
        subject.DeleteAsync("key")  |> Async.AwaitTask |> Async.RunSynchronously
        Assert.Equal(inMemoryStore.getMap().Count,0)
        (fun () -> subject.GetAsync<string>("key").Result |> ignore) |> should throw typeof<System.AggregateException>

    [<Fact>]
    let ``can clear``() =
        subject.StoreAsync("key","val") |> Async.AwaitTask |> Async.RunSynchronously
        subject.ClearAsync() |> Async.AwaitTask |> Async.RunSynchronously
        let x = subject.ClearAsync()  |> Async.AwaitTask |> Async.RunSynchronously
        Assert.Equal(inMemoryStore.getMap().Count,0)

    [<Fact>]
    let ``blows up on type mismatch``() =
        subject.StoreAsync("key",System.DateTime.Now) |> Async.AwaitTask |> Async.RunSynchronously
        (fun () -> subject.GetAsync<string>("key").Result |> ignore) |> should throw typeof<System.AggregateException>
