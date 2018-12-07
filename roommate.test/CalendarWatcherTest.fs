namespace Roommate.Tests

open Xunit
open FsUnit
open Roommate
// open Google.Apis.Util.Store

// open WrappedDataStore
module CalendarWatcherTest =
    // open System.Threading.Tasks
    // open Google.Apis.Util.Store
    open Roommate.CalendarWatcher


    [<Fact>]
    let ``parses calendar ID from URI``() =
        let uri = "https://www.googleapis.com/calendar/v3/calendars/atomicobject.com_234523452345@resource.calendar.google.com/events?maxResults=250&alt=json"
        calIdFromURI uri |> should equal "atomicobject.com_234523452345@resource.calendar.google.com"

    // let calIdFromURI (calURI:string) =
    //     calURI.Split('/') |> List.ofArray |> List.find (fun x -> x.Contains "atomicobject.com")