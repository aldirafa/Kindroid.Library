# Kindroid.Library wiki

Unofficial .NET client for the [Kindroid](https://kindroid.ai) public REST API. Written in VB.NET, usable from any .NET language. Examples in these pages are shown in **VB.NET, F#, C#, then C++/CLI**.

| Page | What it covers |
|---|---|
| [Getting Started](Getting-Started) | Install, API key, client options, first request |
| [Single-AI Chat](Single-AI-Chat) | Send, stream, history, rewind, chat break, update persona |
| [Group Chat](Group-Chat) | Group messages, the turn loop, history, update |
| [Streaming](Streaming) | How streaming behaves, consuming it from each language |
| [Error Handling](Error-Handling) | `KindroidException` family, status codes, validation, rate limits |
| [Server Behaviour](Server-Behaviour) | Quirks observed on the live server (read this!) |
| [Models Reference](Models-Reference) | Request/response types |
| [Migrating to 2.0](Migrating-to-2.0) | Breaking changes from 1.x |

> **Verification status.** 2.0 was exercised against a live Kindroid account (single AI and group) and by 27 offline unit tests. The Discord endpoint was implemented from the docs and is **not** verified live. The F#/C#/VB samples are compiled; the C++/CLI samples are **not** compiled.
