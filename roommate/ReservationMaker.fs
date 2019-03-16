namespace Roommate
open GoogleEventMapper

module ReservationMaker =

    type InputInformation = {
        RoommateAccountEvents : RoommateEvent list
        ConferenceRoomAccountEvents : RoommateEvent list
        RequestedTimeStart : System.DateTime
        RequestedTimeEnd : System.DateTime
    }
    type ProcessResult =
        | CreateNewEvent of System.DateTime * System.DateTime // todo: record type
        | Error of string

    let processInput (input:InputInformation) : ProcessResult =
        CreateNewEvent (input.RequestedTimeStart,input.RequestedTimeEnd)

    ()

