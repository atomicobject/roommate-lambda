namespace Roommate.Tests

open Xunit

// open WrappedDataStore
module WrappedDataStoreTest =
    open System.Threading.Tasks
    open Google.Apis.Util.Store
    open Roommate.WrappedDataStore
    open Xunit

    [<Fact>]
    let ``stores and recalls string``() =
        let subject = new WrappedDataStore() :> IDataStore
        subject.StoreAsync("key","val")
        |> Async.AwaitTask
        |> Async.RunSynchronously
        let x = subject.GetAsync<string>("key").Result
        Assert.Equal(x,"val")
