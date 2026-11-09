# Getting Started

## Requirements
- .NET 10 (the library targets `net10.0`)
- A Kindroid API key (`kn_…`) from your account's Profile Settings. Treat it like a password: it can do anything to your account, including deleting it. Never commit it; use user-secrets or environment variables.

## Install
```
dotnet add package Kindroid.Library
```

## Your first request

### VB.NET
```vb
Imports Kindroid.Library
Imports Kindroid.Library.Exceptions

Using client As New KindroidClient("kn_yourApiKeyHere")
    Try
        Dim reply = Await client.SendMessageAsync("ai_abc123", "Hello!")
        Console.WriteLine(reply)
    Catch ex As KindroidException
        Console.WriteLine($"Kindroid API error {ex.StatusCode}: {ex.ResponseBody}")
    End Try
End Using
```

### F#
```fsharp
open Kindroid.Library
open Kindroid.Library.Exceptions

task {
    use client = new KindroidClient("kn_yourApiKeyHere")
    try
        let! reply = client.SendMessageAsync("ai_abc123", "Hello!")
        printfn "%s" reply
    with :? KindroidException as ex ->
        printfn "Kindroid API error %d: %s" ex.StatusCode ex.ResponseBody
}
```

### C#
```csharp
using Kindroid.Library;
using Kindroid.Library.Exceptions;

using var client = new KindroidClient("kn_yourApiKeyHere");
try
{
    string reply = await client.SendMessageAsync("ai_abc123", "Hello!");
    Console.WriteLine(reply);
}
catch (KindroidException ex)
{
    Console.WriteLine($"Kindroid API error {ex.StatusCode}: {ex.ResponseBody}");
}
```

### C++/CLI *(not compiled)*
```cpp
using namespace Kindroid::Library;
using namespace Kindroid::Library::Exceptions;

KindroidClient^ client = gcnew KindroidClient("kn_yourApiKeyHere");
try {
    String^ reply = client->SendMessage("ai_abc123", "Hello!");
    Console::WriteLine(reply);
} catch (KindroidException^ ex) {
    Console::WriteLine("Kindroid API error {0}: {1}", ex->StatusCode, ex->ResponseBody);
} finally {
    delete client;
}
```

Every operation exists as a blocking method (`SendMessage`) and an async method (`SendMessageAsync`). Reply-generating calls also have a streaming form, see [Streaming](Streaming).

## Client options
```vb
Dim options As New KindroidClientOptions() With {
    .Timeout = TimeSpan.FromMinutes(5),   ' default 10 min; replies take ~4-6 s
    .MaxRetries = 2,                      ' retry HTTP 429 (default 0 = never)
    .RetryDelay = TimeSpan.FromSeconds(15) ' doubles each retry
}
Dim client As New KindroidClient("kn_…", options)
```
| Option | Default | Notes |
|---|---|---|
| `BaseUrl` | `https://api.kindroid.ai/v1` | |
| `Timeout` | 10 minutes | Applies only to the `HttpClient` the library creates. |
| `MaxRetries` | 0 | A 429 means the request was not processed, so retrying is safe for every endpoint. |
| `RetryDelay` | 10 s | Doubled per attempt; a `Retry-After` header wins if the server ever sends one. |

## Bring your own HttpClient
```vb
' Let the library attach the key (recommended):
Dim client As New KindroidClient(httpClient, "kn_…", New KindroidClientOptions())
' Or configure "Authorization: Bearer kn_…" yourself and use New KindroidClient(httpClient)
```
The library never disposes an `HttpClient` you pass in. `options.Timeout` is ignored in this case.

## Where do I get the ids?
`ai_id` and `group_id` are shown in the Kindroid app (profile settings). Groups are created in the app; the API only operates on existing groups, and needs an active subscription (otherwise `403`).
