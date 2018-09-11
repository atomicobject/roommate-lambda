
Currently using *user* google clientId/clientSecret. (we should use a service account in the future)

I neglected to document the detailed process for creating the secrets, but it starts here:
https://console.cloud.google.com/apis

For now we solely expect to find secrets in environment variables. I have an unversioned env.sh to export them, like this:


```
export googleClientId=asdfasdfasdf
export googleClientSecret=jkljkljkl
```

(then you can `source env.sh`)

