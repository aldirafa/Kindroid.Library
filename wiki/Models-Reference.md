# Models Reference

Namespace `Kindroid.Library.Models`. Request types are plain classes with settable properties; `Nothing`/`null` properties are **not sent**.

## Responses

### `GetChatMessagesResponse`
| Property | Type | Notes |
|---|---|---|
| `Messages` | `Collection(Of ChatMessage)` | Oldest first. Never `Nothing`. |
| `Pagination` | `PaginationInfo` | |

### `PaginationInfo`
| Property | Type | Notes |
|---|---|---|
| `HasMore` | `Boolean` | |
| `LastTimestamp` | `Long?` | Pass as `startAfterTimestamp`. `Nothing` on an empty page. Must be a "safe number" (≤ 2^53-1). |
| `Limit` | `Integer` | Page size used. |

### `ChatMessage`
| Property | Type | Notes |
|---|---|---|
| `Id` | `String` | |
| `Sender` | `String` | `"user"` or `"ai"`. |
| `SenderType` | `String` | Documented, never observed → usually empty. |
| `DisplayName` | `String` | Present in groups; may be empty. |
| `Timestamp` | `Long` | Unix **milliseconds**. |
| `Message` | `String` | |
| `ImageUrls` | `Collection(Of String)` | Never `Nothing` (server sends `null`). Entries are **encrypted tokens** (`!enc:…`), not usable URLs. |
| `ImageDescription`, `VideoDescription`, `InternetResponse`, `LinkUrl`, `LinkDescription` | `String` | Empty when absent. Descriptions start with a bracketed `(Attached …)` / `(Context from …)` prefix. |
| `FileName` | `String` | Attached document's name only (no content). Not in Kindroid's published reference. |
| `IsFromUser`, `IsFromAi` | `Boolean` | Computed from `Sender`. |
| `SentAt` | `DateTimeOffset` | Computed from `Timestamp`. |

### `GroupGetTurnResult`
`AiId As String` (`Nothing` when it is the user's turn) and `IsUserTurn As Boolean`.

### `GroupAiReply`
`AiId`, `Text`: yielded by `GroupRunTurnsAsync`. `Text` can be empty.

## Requests
| Type | Required | Optional |
|---|---|---|
| `UpdateAiInfoRequest` | `AiId` | `AiName`, `AiGender`, `AiBackstory`, `AiMemory`, `AiDirective`, `AiExampleMessage`, `AiAdditionalContext`, `CurrentScene`, `UserName`, `UserGender` |
| `UpdateGroupInfoRequest` | `GroupId` | `AiList` (≥ 1 id), `GroupName`, `GroupContext`, `GroupDirective`, `CurrentScene` |
| `DiscordBotRequest` | `ShareCode`, `Conversation` (≥ 1 `DiscordConversationMessage`) | `EnableFilter` (default `True`) |

`SendMessageRequest`, `ChatBreakRequest`, `RewindMessagesRequest`, `GroupUserMessageRequest`, `GroupGetTurnRequest`, `GroupAiResponseRequest`, `GroupChatBreakRequest` are the wire bodies. The client builds them for you; you normally do not touch them.

## Discord endpoint *(not verified live)*
```vb
Dim req As New DiscordBotRequest() With {.ShareCode = "abcde", .EnableFilter = True}
req.Conversation.Add(New DiscordConversationMessage() With {
    .Username = "alice", .Text = "hi!", .Timestamp = DateTime.UtcNow.ToString("o")})
Dim reply = Await client.DiscordBotAsync(req, KindroidClient.HashRequester("alice"))
```
`HashRequester` mirrors Kindroid's JavaScript sample for the `X-Kindroid-Requester` header.
