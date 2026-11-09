# Streaming

`SendMessageStreamAsync` and `GroupAiResponseStreamAsync` return an `IAsyncEnumerable(Of String)` of text chunks as the AI generates them (first byte after ~1-3 s).

## What the server actually sends
- `Content-Type: text/event-stream`, but the body is **plain text, not SSE** (no `data:` lines).
- Chunks are arbitrary token fragments (often 1-3 characters of CJK text).
- **Whitespace-only chunks are real text.** The first chunk of a reply was observed to be a single space. The library never filters chunks; concatenate them exactly as received.
- Characters can be split across network reads; the library reassembles them.
- Errors detected before generation (`400`, `429`, …) arrive as a normal status code with a plain-text body, and are thrown as `KindroidException` when you start enumerating.

The sequence is **lazy**: nothing is sent until you start enumerating. Cancel with the token you pass in, or simply stop and dispose; disposing mid-stream aborts the HTTP request.

## Consuming it

### VB.NET
Visual Basic has no `Await ForEach`, **and its compiler rejects `Await` inside `Finally` (error BC36943)**, so the manual `GetAsyncEnumerator` loop with `Finally Await DisposeAsync()` (shown in 1.x docs) does not compile. Use the extension methods instead:
```vb
Imports Kindroid.Library

' Print as it arrives
Await client.SendMessageStreamAsync("ai_abc123", "Tell me a story.").
    ForEachAsync(Sub(chunk) Console.Write(chunk))

' Async handler (the next chunk is not requested until it completes)
Await client.SendMessageStreamAsync("ai_abc123", "Hi").
    ForEachAwaitAsync(Function(chunk) outputStream.WriteAsync(chunk))

' Whole reply as a string
Dim full As String = Await client.SendMessageStreamAsync("ai_abc123", "Hi").ReadAllTextAsync()
```
### F#
```fsharp
do! client.SendMessageStreamAsync("ai_abc123", "Tell me a story.")
          .ForEachAsync(fun chunk -> Console.Write chunk)
let! full = client.SendMessageStreamAsync("ai_abc123", "Hi").ReadAllTextAsync()
```
### C#
```csharp
await foreach (var chunk in client.SendMessageStreamAsync("ai_abc123", "Tell me a story."))
    Console.Write(chunk);

// cancellation
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await foreach (var chunk in client.SendMessageStreamAsync("ai_abc123", "Hi").WithCancellation(cts.Token))
    Console.Write(chunk);
```
### C++/CLI *(not compiled)*
C++/CLI cannot `await foreach`; use the extension method with a delegate:
```cpp
void Print(String^ chunk) { Console::Write(chunk); }
...
AsyncEnumerableExtensions::ForEachAsync(
    client->SendMessageStreamAsync("ai_abc123", "Tell me a story.", CancellationToken()),
    gcnew Action<String^>(&Print), CancellationToken())->GetAwaiter().GetResult();
```

## Rendering note
Replies use `*action*` and `**bold**` markup, and may contain `[` `]`. Escape before handing text to markup-based UIs (the `Test` project does this for Spectre.Console).

## Rate limiting while streaming
A stream is one request. See [Error Handling](Error-Handling#rate-limits) for 429 behaviour.
