# Kindroid.Library

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/Y0F020WAJC)

`Kindroid.Library` is an unofficial .NET client library for the [Kindroid AI](https://kindroid.ai) public REST API written in Visual Basic .NET. It wraps every documented endpoint — single-AI chat, group chat, streaming replies, and chat history management — in a single `KindroidClient` class, with each operation exposed in **synchronous**, **async**, and (where supported) **streaming** form.

> **Note:** This is a third-party / community client, not an official Kindroid SDK.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com/)
[![Language](https://img.shields.io/badge/Language-VB.NET-purple)](https://learn.microsoft.com/dotnet/visual-basic/)

📖 **Full documentation lives in the [Wiki](../../wiki)** — this README only covers the basics.

---

## Features

- ✅ Single-AI messaging — send, stream, reset (chat break), rewind, and update persona/config
- ✅ Group chat support — user messages (text or voice), turn-taking, AI responses (sync/async/stream), chat break, roster/config updates
- ✅ Real streaming — `IAsyncEnumerable(Of String)` for token-by-token replies, backed by `System.Threading.Channels`
- ✅ Paginated chat history retrieval for both single-AI and group conversations
- ✅ A dedicated `KindroidException` with HTTP-status-aware messages (401, 403, 404, 429, 500, …)
- ✅ Bring-your-own `HttpClient` support for DI / connection-pooling scenarios

## Requirements

- .NET 10.0 (the project's TargetFramework is net10.0, so consuming projects need to target .NET 10 too)
- A Kindroid API key (`kn_…`) — generate one from your Kindroid account settings

## Installation

**Option A — Project reference**

Clone or download this repository and add `Kindroid.Library` as a project reference in your solution.

```
gh repo clone aldirafa/Kindroid.Library
```

**Option B — NuGet**

```
dotnet add package Kindroid.Library
```


## Quickstart

```vb
Imports Kindroid.Library
Imports Kindroid.Library.Exceptions

Dim client As New KindroidClient("kn_yourApiKeyHere")

Try
    ' Synchronous
    Dim reply = client.SendMessage("ai_abc123", "Hello!")
    Console.WriteLine(reply)

    ' Async
    Dim asyncReply = Await client.SendMessageAsync("ai_abc123", "Hello again!")

    ' Streaming
    Dim enumerator = client.SendMessageStreamAsync("ai_abc123", "Tell me a story.").GetAsyncEnumerator()
    Try
        While Await enumerator.MoveNextAsync()
            Console.Write(enumerator.Current)
        End While
    Finally
        Await enumerator.DisposeAsync()
    End Try

Catch ex As KindroidException
    Console.WriteLine($"Kindroid API error {ex.StatusCode}: {ex.Message}")
End Try
```

> 💡 VB.NET has no `Await foreach` like C#. See [Streaming](../../wiki/Streaming) in the wiki for why, and for the manual-enumerator pattern used throughout this library.

`KindroidClient` implements `IDisposable` — wrap it in a `Using` block, or dispose it when your app shuts down, so its internal `HttpClient` is released.

## Documentation

| Page | Description |
|---|---|
| [Getting Started](../../wiki/Getting-Started) | Installation, API keys, first request |
| [Single-AI Chat](../../wiki/Single-AI-Chat) | Send/stream messages, chat break, history, rewind, update info |
| [Group Chat](../../wiki/Group-Chat) | Multi-AI group conversations, turn-taking |
| [Streaming](../../wiki/Streaming) | How streaming works, and how to consume it in VB.NET |
| [Error Handling](../../wiki/Error-Handling) | `KindroidException` and status-code meanings |
| [Models Reference](../../wiki/Models-Reference) | Every request/response DTO, field by field |

## License

This project is made available under the [MIT License](LICENSE).

## Contributing

Issues and pull requests are welcome. Since this wraps a third-party API, please double-check any endpoint-behavior changes against the [official Kindroid API docs](https://docs.kindroid.ai) before submitting.
