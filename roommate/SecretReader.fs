namespace Roommate

module SecretReader =
    open System

    type MySecretType = string
    
    type RoommateSecrets = {
        googleClientId : string
        googleClientSecret : string
        calendarIds : string option
    }
    
    let secretOrBust s =
        let envVar = Environment.GetEnvironmentVariable s
        match envVar with
        | null -> failwith (sprintf "secret %s not found." s) 
        | "" -> failwith (sprintf "secret %s not found." s)
        | e -> e
        
    let optionalSecret s = 
        let envVar = Environment.GetEnvironmentVariable s
        match envVar with
        | null -> None
        | "" -> None
        | e -> Some e
        
    let readSecrets secretName =

        {
            googleClientId = secretOrBust "googleClientId"
            googleClientSecret = secretOrBust "googleClientSecret"
            calendarIds = optionalSecret "CALENDAR_IDS"
        }
            
                            
