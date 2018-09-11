namespace RoommateLambda

module SecretReader =
    open System

    type MySecretType = string
    
    type RoommateSecrets = {
        googleClientId : string
        googleClientSecret : string
    }
    
    let secretOrBust s =
        let envVar = Environment.GetEnvironmentVariable s
        match envVar with
        | null -> failwith (sprintf "secret %s not found." s) 
        | "" -> failwith (sprintf "secret %s not found." s)
        | _ -> envVar
        
    let readSecrets secretName =

        {
            googleClientId = secretOrBust "googleClientId"
            googleClientSecret = secretOrBust "googleClientSecret"
        }
            
                            
