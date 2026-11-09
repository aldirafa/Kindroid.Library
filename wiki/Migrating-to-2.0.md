# Migrating from 1.x to 2.0

2.0 is the first version checked against the live server. Several 1.x behaviours were bugs, so some code that "worked" before will now surface errors that used to be hidden.

## 1. Errors now throw
In 1.x a failed call (`400`, `404`, `429`, …) did **not** throw: the error text came back as the "reply". Now every non-2xx throws `KindroidException`. Wrap calls and handle `KindroidRateLimitException` (see [Error Handling](Error-Handling)).

## 2. Streaming
- Chunks are no longer filtered: whitespace-only chunks (including the leading space of a reply) are delivered. Don't `Trim()` per chunk.
- The sequence is lazy and always terminates: errors are thrown from enumeration instead of hanging.
- The VB `Finally Await enumerator.DisposeAsync()` pattern never compiled. Replace the manual loop:

```vb
' 1.x docs (does not compile)
Dim e = client.SendMessageStreamAsync(id, msg).GetAsyncEnumerator()
Try
    While Await e.MoveNextAsync() : Console.Write(e.Current) : End While
Finally
    Await e.DisposeAsync()
End Try

' 2.0
Await client.SendMessageStreamAsync(id, msg).ForEachAsync(Sub(c) Console.Write(c))
```

## 3. Types
| 1.x | 2.0 |
|---|---|
| `PaginationInfo.LastTimestamp As Long` | `As Long?` (`Nothing` on an empty page) |
| `KindroidException` only | plus `KindroidAuthenticationException`, `…ForbiddenException`, `…NotFoundException`, `…RateLimitException` (still derive from `KindroidException`) |
| `New KindroidClient(httpClient)` | still works; new `New KindroidClient(httpClient, apiKey, options)` |

## 4. Validation
These now throw `ArgumentException`/`ArgumentOutOfRangeException` before sending: blank ids, empty message/greeting, `limit` outside 1-100, single-AI `RewindMessages` with an odd or < 2 count, empty `AiList`.

## 5. Timeout
Default request timeout is 10 minutes (was `HttpClient`'s 100 s). Change via `KindroidClientOptions.Timeout`.

## New in 2.0
`KindroidClientOptions` (retry on 429), `ForEachAsync`/`ReadAllTextAsync`, `EnumerateChatMessagesAsync`, `GroupRunTurnsAsync`, `DiscordBot`, `ChatMessage.SentAt`/`IsFromUser`/`IsFromAi`.
