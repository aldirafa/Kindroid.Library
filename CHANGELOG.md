# Changelog

## 2.0.0

First release verified against the live Kindroid server. Several 1.x behaviours were outright bugs, so this is a **major, breaking** release. Observed server quirks are documented in the wiki page *Server Behaviour*.

### Fixed (these change behaviour)
- **API errors now throw.** In 1.x a 4xx/5xx response never raised `KindroidException`; the error text (e.g. `AI not found`) was returned as if it were the AI's reply. Every non-2xx now throws, and the failed response is disposed.
- **Chat history now works.** `image_urls: null` and `pagination.lastTimestamp: null` (empty page) made deserialization fail or yield no messages.
- **Streaming can no longer hang.** Any exception (HTTP error, network failure, cancellation) is delivered to the consumer; 1.x only forwarded `KindroidException`, leaving the reader waiting forever.
- **Streaming no longer corrupts text.** Whitespace-only chunks (the server's first chunk is a single space) were dropped. Multi-byte characters split between reads are reassembled.
- Disposing a stream enumerator early now cancels the request.
- Argument validation actually runs (it was unreachable for chat history).

### Changed (breaking)
- `PaginationInfo.LastTimestamp` is now `Long?`.
- `KindroidException`: message format changed; added `RetryAfter`; 401/403/404/429 are the subclasses `KindroidAuthenticationException`, `KindroidForbiddenException`, `KindroidNotFoundException`, `KindroidRateLimitException` (all still catchable as `KindroidException`).
- Invalid arguments now throw `ArgumentException` / `ArgumentOutOfRangeException` before any request: blank ids, empty messages/greetings, `limit` outside 1-100, odd or `< 2` single-AI rewind counts, `AiList` empty.
- Streaming methods are lazy: the request is sent when enumeration starts.
- `ChatMessage.ImageUrls` and `GetChatMessagesResponse.Messages` have setters (never `Nothing`).
- `HttpClient` constructor no longer assumes the caller set the header when you use the new overload that takes the key.

### Added
- `KindroidClientOptions`: `BaseUrl`, `Timeout` (default 10 min, was 100 s), `MaxRetries`/`RetryDelay` for HTTP 429.
- `ForEachAsync`, `ForEachAwaitAsync`, `ReadAllTextAsync` extensions: Visual Basic cannot `Await` inside `Finally` (BC36943), so the manual enumerator pattern shown in 1.x docs did not compile.
- `EnumerateChatMessagesAsync` / `EnumerateGroupChatMessagesAsync` (paging), `GroupRunTurnsAsync` (turn loop).
- `ChatMessage.IsFromUser`, `IsFromAi`, `SentAt`, and `ChatMessage.FileName` (attached document name; observed live, absent from Kindroid's reference).
- `DiscordBot` / `DiscordBotAsync` and `HashRequester` (implemented from the docs; **not verified live**).
- `Library.Tests` (xUnit, offline) and live probe harness in `Test` (`dotnet run --project Test -- probe <scenario>`).

## 1.0.0
Initial release.
