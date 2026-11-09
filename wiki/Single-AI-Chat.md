# Single-AI Chat

All methods take an `aiId`. Each has a sync and an `…Async` form; `cancellationToken` is always the last optional argument.

| Operation | Method | Notes |
|---|---|---|
| Send a message | `SendMessage` / `SendMessageAsync` | Returns the full reply (≈4-6 s). Empty message is rejected; whitespace-only is accepted by the server. |
| Stream a reply | `SendMessageStreamAsync` | See [Streaming](Streaming). |
| Read history | `GetChatMessages(Async)`, `EnumerateChatMessagesAsync` | Oldest first, 600 requests / 24 h. |
| Undo | `RewindMessages(Async)` | `count` must be **even and ≥ 2**. |
| New chat | `ChatBreak(Async)` | Greeting required. `wipeCascaded:=True` permanently erases long-term memory. |
| Edit persona | `UpdateAiInfo(Async)` | Send only what you change. |

## Send

### VB.NET
```vb
Dim reply = Await client.SendMessageAsync("ai_abc123", "How was your day?")
```
### F#
```fsharp
let! reply = client.SendMessageAsync("ai_abc123", "How was your day?")
```
### C#
```csharp
string reply = await client.SendMessageAsync("ai_abc123", "How was your day?");
```
### C++/CLI *(not compiled)*
```cpp
String^ reply = client->SendMessage("ai_abc123", "How was your day?");
```

## History
History is returned **oldest first**, and a request without a cursor always returns the *oldest* page. To reach recent messages you page forward with `start_after_timestamp`; `EnumerateChatMessagesAsync` does this for you (use the default page size of 100 to spend as few of the 600 daily requests as possible). An unknown `aiId` returns an **empty page, not an error**.

### VB.NET
```vb
' One page
Dim page = Await client.GetChatMessagesAsync("ai_abc123", limit:=50)
For Each m In page.Messages
    Console.WriteLine($"{m.SentAt:u} {If(m.IsFromUser, "me", m.DisplayName)}: {m.Message}")
Next
' Next page
If page.Pagination.HasMore Then
    Dim nextPage = Await client.GetChatMessagesAsync("ai_abc123", 50, page.Pagination.LastTimestamp)
End If

' Everything
Await client.EnumerateChatMessagesAsync("ai_abc123").ForEachAsync(
    Sub(m) Console.WriteLine(m.Message))
```
### F#
```fsharp
let! page = client.GetChatMessagesAsync("ai_abc123", Nullable 50)
for m in page.Messages do
    printfn "%s: %s" m.Sender m.Message

do! client.EnumerateChatMessagesAsync("ai_abc123")
          .ForEachAsync(fun m -> printfn "%s" m.Message)
```
### C#
```csharp
var page = await client.GetChatMessagesAsync("ai_abc123", limit: 50);
foreach (var m in page.Messages) Console.WriteLine($"{m.SentAt:u} {m.Sender}: {m.Message}");

await foreach (var m in client.EnumerateChatMessagesAsync("ai_abc123"))
    Console.WriteLine(m.Message);
```
### C++/CLI *(not compiled)*
```cpp
GetChatMessagesResponse^ page = client->GetChatMessages("ai_abc123", 50);
for each (ChatMessage^ m in page->Messages)
    Console::WriteLine("{0}: {1}", m->Sender, m->Message);
```

Message fields: `Sender` is `"user"` or `"ai"` (`IsFromUser`/`IsFromAi`); `DisplayName` may be empty; `ImageUrls` is never `Nothing`; `SentAt` converts the millisecond `Timestamp`. `SenderType` is documented by Kindroid but was never observed in responses.

> `ChatBreak` does **not** remove earlier messages from history; the greeting is appended.

## Rewind (undo)
```vb
Await client.RewindMessagesAsync("ai_abc123", 2)   ' removes the last user+AI exchange
```
Odd counts are rejected locally with `ArgumentOutOfRangeException` (the server would answer `400` because the chat must end on an AI message).

## Chat break
```vb
Await client.ChatBreakAsync("ai_abc123", "Good morning!")                       ' short-term memory only
Await client.ChatBreakAsync("ai_abc123", "Fresh start.", wipeCascaded:=True)    ' PERMANENT
```

## Update persona
Only non-`Nothing` fields are sent; an empty string clears the field.
```vb
Await client.UpdateAiInfoAsync(New UpdateAiInfoRequest() With {
    .AiId = "ai_abc123",
    .CurrentScene = "A quiet rainy cafe.",
    .UserName = "Al"
})
```
```csharp
await client.UpdateAiInfoAsync(new UpdateAiInfoRequest { AiId = "ai_abc123", CurrentScene = "A quiet rainy cafe." });
```
```fsharp
do! client.UpdateAiInfoAsync(UpdateAiInfoRequest(AiId = "ai_abc123", CurrentScene = "A quiet rainy cafe."))
```
