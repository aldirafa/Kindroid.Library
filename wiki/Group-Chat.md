# Group Chat

Group chats need an active Kindroid subscription (otherwise `403` → `KindroidForbiddenException`). Create and configure groups in the app; the API works on an existing `group_id`.

| Operation | Method |
|---|---|
| Post a user message | `GroupUserMessage(Async)` (text) / `GroupUserAudioMessage(Async)` (audio URL) |
| Whose turn? | `GroupGetTurn(Async)` |
| AI reply | `GroupAiResponse(Async)`, `GroupAiResponseStreamAsync` |
| Whole turn loop | `GroupRunTurnsAsync` |
| History | `GetGroupChatMessages(Async)`, `EnumerateGroupChatMessagesAsync` |
| Undo | `RewindGroupMessages(Async)`: **any** count ≥ 1 (no even rule) |
| New chat | `GroupChatBreak(Async)` |
| Edit group | `UpdateGroupInfo(Async)` |

## The turn loop
1. Post the user's message.
2. Ask `GroupGetTurn(allowUser:=True)`. An AI id → generate its reply; **empty = the user's turn**, stop.
3. Repeat from 2.

`GroupRunTurnsAsync` does steps 2-3 for you and yields each reply (capped by `maxTurns`, default 10, as a safety net).

### VB.NET
```vb
Await client.GroupUserMessageAsync("group_1", "Good evening, everyone.")
Await client.GroupRunTurnsAsync("group_1").ForEachAsync(
    Sub(r) Console.WriteLine($"{r.AiId}: {r.Text}"))
```
### F#
```fsharp
do! client.GroupUserMessageAsync("group_1", "Good evening, everyone.")
do! client.GroupRunTurnsAsync("group_1")
          .ForEachAsync(fun r -> printfn "%s: %s" r.AiId r.Text)
```
### C#
```csharp
await client.GroupUserMessageAsync("group_1", "Good evening, everyone.");
await foreach (var r in client.GroupRunTurnsAsync("group_1"))
    Console.WriteLine($"{r.AiId}: {r.Text}");
```
### C++/CLI *(not compiled)*
```cpp
client->GroupUserMessage("group_1", "Good evening, everyone.");
GroupGetTurnResult^ turn = client->GroupGetTurn("group_1", true);
while (!turn->IsUserTurn) {
    Console::WriteLine("{0}: {1}", turn->AiId, client->GroupAiResponse("group_1", turn->AiId));
    turn = client->GroupGetTurn("group_1", true);
}
```

### Manual loop with streaming (VB.NET)
```vb
Dim turn = Await client.GroupGetTurnAsync("group_1")
Do Until turn.IsUserTurn
    Console.Write(turn.AiId & ": ")
    Await client.GroupAiResponseStreamAsync("group_1", turn.AiId).ForEachAsync(Sub(c) Console.Write(c))
    Console.WriteLine()
    turn = Await client.GroupGetTurnAsync("group_1")
Loop
```

## Quirks
- `allowUser:=False` makes the server return an AI even when it would be the user's turn, so the loop would never end by itself (hence `maxTurns`).
- An AI id that exists but is **not in the group** gets `200` with an **empty** reply; an unknown id gets `400 Invalid AI ID.` A group member was also seen returning an empty reply once, so treat `""` as "nothing to show", not as failure.
- Voice messages: the URL must be reachable audio. A bad URL makes the server answer `500 Server error while transcribing`.
- `UpdateGroupInfo`: `AiList` must contain ≥ 1 id and every id must be valid (`400 AI list is not valid`).
- Unknown group ids are `400` (`Invalid group ID.` / `Group not found`), not `404`.
- Group history items carry `DisplayName` (the AI's or your name); `Sender` is `"user"` or `"ai"`.
