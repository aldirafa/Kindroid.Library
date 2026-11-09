# Error Handling

## Exceptions
Every non-2xx response throws a `KindroidException` (or subclass) carrying `StatusCode` and the server's plain-text `ResponseBody`.

| Class | Status | Typical body |
|---|---|---|
| `KindroidAuthenticationException` | 401 | `Unauthorized` |
| `KindroidForbiddenException` | 403 | group chat without subscription |
| `KindroidNotFoundException` | 404 | `AI not found` (rewind, update-info) |
| `KindroidRateLimitException` | 429 | `Too Many Requests (API limit)` |
| `KindroidException` | 400, 500, other | validation text, `AI not found`, `Invalid group ID.` … |

Note the server is inconsistent: an unknown AI is `400` for send/chat-break, `404` for rewind/update-info, and an **empty `200`** for history. Don't rely on 404 to detect "doesn't exist".

### VB.NET
```vb
Try
    Dim reply = Await client.SendMessageAsync(aiId, text)
Catch ex As KindroidRateLimitException
    ' back off, then retry
Catch ex As KindroidAuthenticationException
    ' bad / revoked key
Catch ex As KindroidException
    Console.WriteLine($"{ex.StatusCode}: {ex.ResponseBody}")
End Try
```
### F#
```fsharp
try
    let! reply = client.SendMessageAsync(aiId, text)
    ()
with
| :? KindroidRateLimitException -> () // back off
| :? KindroidException as ex -> printfn "%d: %s" ex.StatusCode ex.ResponseBody
```
### C#
```csharp
try { var reply = await client.SendMessageAsync(aiId, text); }
catch (KindroidRateLimitException) { /* back off */ }
catch (KindroidException ex) { Console.WriteLine($"{ex.StatusCode}: {ex.ResponseBody}"); }
```
### C++/CLI *(not compiled)*
```cpp
try { client->SendMessage(aiId, text); }
catch (KindroidRateLimitException^) { /* back off */ }
catch (KindroidException^ ex) { Console::WriteLine("{0}: {1}", ex->StatusCode, ex->ResponseBody); }
```

Network failures (`HttpRequestException`), timeouts (`TaskCanceledException`) and your own cancellation (`OperationCanceledException`) are **not** wrapped; they propagate as-is, including from streams.

## Argument validation
Obvious mistakes are rejected before a request is sent (saving rate-limit budget): blank ids, empty message/greeting, `limit` outside 1-100, single-AI rewind count that is odd or < 2, group rewind count < 1, empty `AiList`. They throw `ArgumentException` / `ArgumentOutOfRangeException`.

## Rate limits
- The limiter is **per API key** and runs before routing: while you are limited, *every* request (even to unknown paths) returns `429`.
- It was tripped by a burst of ~12 quick requests, and by rapid paging with tiny page sizes. It cleared in well under two minutes. The server sends **no `Retry-After`/rate-limit headers**, so `RetryAfter` is normally `Nothing`.
- `get-chat-messages` additionally allows only **600 requests / 24 h**.
- Mitigation: space calls out, use `pageSize` 100, and optionally let the library retry 429s:
```vb
Dim client As New KindroidClient(key, New KindroidClientOptions() With {.MaxRetries = 3, .RetryDelay = TimeSpan.FromSeconds(20)})
```
Retries are safe because a 429 means the request was not processed.

## Other server errors
A malformed request body gives `500 text/html: An unexpected error occurred`; the library always sends valid JSON, so you should only see this for bugs.
