# Server Behaviour (observed)

Captured with raw `HttpClient` probes (`dotnet run --project Test -- probe <scenario>`, see `Test/Probe.vb` in the repository) against a dedicated test AI and group on 2026-11-09. These are observations, not documented guarantees.

## Transport
| Topic | Observation |
|---|---|
| Content-Type | **Everything is `text/plain; charset=utf-8`, including JSON** (`get-chat-messages`). Never rely on content type to detect JSON. Streaming replies are `text/event-stream` but the body is **raw text, not SSE framing** (no `data:` lines). |
| Success bodies | Write endpoints (`chat-break`, `update-info`, `groupchats-user-message`, `groupchats-update`, `groupchats-chat-break`, rewind) return `200` with body `OK`. |
| Error bodies | Plain text, short. Validation errors are Joi-style (`"limit" must be greater than or equal to 1`). |
| Rate limiting | Per **API key**, evaluated before routing (even unknown paths get 429). A burst of ~12 requests in a few seconds, then a `send-message`, tripped `429 Too Many Requests (API limit)` on everything. Cleared in well under ~2 minutes (a paced run kept failing for ~50 s; a later poll found it clear). Paging with tiny page sizes (several rapid `get-chat-messages`) tripped it too, so prefer `pageSize=100`. **No `Retry-After` / rate-limit headers are sent.** Pacing requests ~3 s apart avoided it. `get-chat-messages` additionally has the documented 600 req/24 h cap. |
| Malformed JSON | `500` with `text/html`, body `An unexpected error occurred`. |
| Unknown path | `404` `text/html` (Express "Cannot GET ..."). |
| Auth | Bad or missing key -> `401`, body `Unauthorized`. |
| Latency | `send-message` 4-6 s; streaming time-to-first-byte ~1-3 s. |

## Streaming (`stream: true`)
- Chunks are arbitrary token fragments (often 1-3 CJK characters). **Whitespace-only chunks occur** (the first chunk was a single space `" "`), so a client must never drop them. UTF-8 sequences may in principle split across reads; use a stateful decoder.
- The reply text can begin with a leading space.
- Errors that occur before generation (400, 429) arrive as a normal non-200 status with a plain-text body, not as a stream.

## get-chat-messages
- Oldest first. **Without a cursor you always get the oldest page**, so reading the latest messages means paging through everything (`hasMore`/`lastTimestamp`). Budget is 600 req/24 h.
- Verified on an 827-message single-AI chat: 9 requests at `limit=100`, no duplicate ids, strictly ascending timestamps, cursor paging ended with `hasMore=false` (the final page still has a non-null `lastTimestamp`; only an *empty* page has `null`). Paced at 3 s, no 429.
- `timestamp` is Unix ms. `start_after_timestamp` is exclusive.
- `pagination.lastTimestamp` is **`null`** when the page is empty; non-null otherwise. `hasMore` is false on the last page.
- **Attachments (verified live on the test AI):** extra fields on *user* messages:
  - image: `image_urls` is an array of **encrypted tokens** (`"!enc:U2FsdGVk..."`, a CryptoJS-style blob), *not* browsable URLs, plus `image_description` (text starting with `(Attached image: ...`).
  - video: `video_description` only (`image_urls` stays `null`); no URL is returned.
  - link: `link_url` and `link_description` (starts with `(Attached link: <url>; link summary: ...`, can be long).
  - document: **`file_name` only** (e.g. `TUGAS 1.docx`). This field is not in Kindroid's published reference, and no content or summary is returned.
  - internet: `internet_response` (starts with `(Context from internet: ...`).
  The description fields start with a newline/spaces and a bracketed prefix.
- Per message: `sender` is `"ai"` or `"user"`; **`sender_type` was not present**; `display_name` is present in group chats and for user/ai messages in single chats (omitted on the first AI greeting). `image_urls` is **`null` when no image is attached** (seen on 411 of 411 user messages in a long chat), is absent on AI messages, and was a non-empty array on only 4 messages (each with `image_description`); it was never `[]`. `sender_type` never appeared in any response (documented, never observed). Absent optional fields are omitted. Longest message seen: 3,624 characters.
- Unknown `ai_id` -> **`200` with empty `messages`** (no error!). Unknown group -> `400 Invalid group ID.` (on other endpoints).
- `limit` outside 1-100 or non-numeric -> `400` (`"limit" must be ...`). Both `ai_id`+`group_id`, or neither -> `400`.
- `chat-break` does **not** remove earlier messages from history; the greeting is appended.

## send-message
- Empty message -> `400 "message" is not allowed to be empty`. **Whitespace-only is accepted.** Missing fields -> `400 "ai_id" is required` / `"message" is required`.
- Unknown AI -> `400 AI not found`.

## rewind-messages
- `count` < 1 -> `400`. Odd count for a single AI -> `400 Rewinding that many messages would leave the chat without an AI response at the end`.
- Unknown AI -> **`404 AI not found`** (note: other endpoints use 400).
- Both ids / neither -> `400`.

## chat-break / groupchats-chat-break
- `greeting` required and non-empty. Unknown AI -> `400 AI not found`; unknown group -> `400 Group not found`.
- `wipe_cascaded: true` accepted (`200 OK`).

## update-info / groupchats-update
- Only ai_id/group_id -> `200 OK` (no-op). Empty string for a field is accepted (clears it). Unknown field -> `400 "bogus_field" is not allowed`.
- Unknown AI -> `404 AI not found.`; unknown group -> `400 Invalid group ID.`
- `ai_list: []` -> `400 "ai_list" must contain at least 1 items`; unknown ids in `ai_list` -> `400 AI list is not valid. Try again later.`

## Group chat
- `groupchats-user-message`: exactly one of `message` / `audio_url` (neither or both -> `400`). An unreachable/invalid audio URL -> **`500 Server error while transcribing – please try again.`** Success -> `200 OK`.
- `groupchats-get-turn`: `allow_user` is **required** (`400` otherwise). Body is the bare `ai_id` (no quotes/JSON); **empty body = user's turn**. With `allow_user:false` an AI id is returned even when the user would be next.
- `groupchats-ai-response`: unknown AI id -> `400 Invalid AI ID.`. An AI that exists but is **not in the group** returned `200` with an **empty** body (no error) – callers should treat an empty reply as suspicious.
- Streaming works the same as single AI (`text/event-stream`, raw text).
