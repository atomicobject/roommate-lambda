namespace Roommate

module SecretReader =
    open System

    // let secretOrBust s =
    //     let envVar = Environment.GetEnvironmentVariable s
    //     match envVar with
    //     | null -> failwith (sprintf "secret %s not found. (did you set the env var?)" s)
    //     | "" -> failwith (sprintf "secret %s not found. (did you set the env var?)" s)
    //     | e -> e
        
    let readEnvVar = Environment.GetEnvironmentVariable >> Option.ofObj
    
    let readSecretOrBust readSecret s =
        match readSecret s with
        | None -> failwith (sprintf "secret %s not found. (did you set the env var?)" s)
        | Some v -> v
        
    let readSecretFromEnv = readSecretOrBust readEnvVar
    