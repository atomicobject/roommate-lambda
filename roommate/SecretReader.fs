namespace Roommate

module SecretReader =
    open System

    type MySecretType = string
    
    // todo: discriminated union? different secrets for different auth mechanisms?
    type RoommateSecrets = {
        googleClientId : string
        googleClientSecret : string
        calendarIds : string option
        accessToken : string
        refreshToken : string
        fullJson : string
    }
    
    let secretOrBust s =
        let envVar = Environment.GetEnvironmentVariable s
        match envVar with
        | null -> failwith (sprintf "secret %s not found. (did you set the env var?)" s)
        | "" -> failwith (sprintf "secret %s not found. (did you set the env var?)" s)
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
            accessToken = secretOrBust "googleClientAccessToken"
            refreshToken = secretOrBust "googleClientRefreshToken"
            fullJson = secretOrBust "fullJson"
        }
            
                            
