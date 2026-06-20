Reference for the endpoints we openly support for `kn_` API-key integrations.

### Base URL

https://api.kindroid.ai/v1

### Authentication

All endpoints require authentication. Send your API key in the `Authorization
`header as a Bearer token:

Authorization: Bearer kn_xxxxxxxxxxxxxxxxxxxxxxxx

Your API key (starts with `kn_...`) and your AI ID can be found in **Profile Settings.**

> **WARNING:** You should only play around with the API if you're a developer
interested in tinkering with integrating Kindroid. **DO NOT** share your key
with anyone who asks, and unless it comes from admins do not trust other
sources. Someone with your API key could do anything to your account,
including deleting it.

### Conventions

- **Identifiers.** `ai_id` refers to a single Kindroid; `group_id` refers to a group chat. Endpoints that operate on a conversation take one or the other.
- **Responses.** Unless otherwise noted, responses are returned as `text/plain`. Endpoints that return structured data (e.g. message history) return JSON, as documented per endpoint.
- **Streaming.** Endpoints that generate an AI reply accept an optional `"stream": true`. When set, the response text is streamed as it is generated instead of returned in one blocking response. Omit it (or set `false`) to receive the full reply once generation completes.
- **Errors.** Failures return one of: `400 Bad Request`, `401 Unauthorized`, `403 Forbidden`, `429 Too Many Requests` (rate limit), `500 Internal Server Error`. The response body contains a short plain-text description.

## Endpoints

### Send Message

Sends a message to an AI and receives a response. This request may take a while,
so you should await its response.

- **URL:** `/send-message`
- **Method:** POST
- **Request Body:**
```
{
  "ai_id": "string",
  "message": "string",
  "stream": false
}
```

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `ai_id` | string | yes | The AI to message. |
| `message` | string | yes | The user's message. |
| `stream` | boolean | no | Stream the reply as it is generated. |
- **Response:**
- Success: `200 OK` with the AI's response as text. With `"stream": true`, the text is streamed as it is generated.
  - Error: `400`, `401`, `403`, `429`, `500`
### Chat Break

Ends the current chat and resets the AI's short-term memory. A greeting is
mandatory and becomes the first message in the new conversation.

- **URL:** `/chat-break`
- **Method:** POST
- **Request Body:**
```
{
  "ai_id": "string",
  "greeting": "string",
  "wipe_cascaded": false
}
```

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `ai_id` | string | yes | The AI to reset. |
| `greeting` | string | yes | First message of the new conversation. |
| wipe_cascaded | boolean | no | Also wipe the AI's cascaded long-term memory built up from previous conversations. Defaults to `false` (only short-term memory is reset). |
- **Response:**
- Success: `200 OK`
  - Error: `400`, `401`, `403`, `429`, `500`
### Get Chat Messages

Retrieves chat history for a Kindroid or a group chat, oldest first, with cursor
pagination. 

- **URL:** `/get-chat-messages`
- **Method:** GET
- **Query Parameters:**
| Parameter | Type | Required | Description |
| --- | --- | --- | --- |
| `ai_id` | string | one of | The AI whose history to fetch. Mutually exclusive with `group_id`. |
| `group_id` | string | one of | The group chat whose history to fetch. Mutually exclusive with `ai_id`. |
| `limit` | number | no | Page size, 1–100. Defaults to 50. |
| `start_after_timestamp` | number | no | Cursor; pass the previous page's `lastTimestamp` to continue. |

Exactly one of `ai_id` or `group_id` must be supplied.
- **Response:**
- Success: `200 OK` with JSON:
```
{
  "messages": [
    {
      "id": "string",
      "sender": "string",
      "sender_type": "string",
      "display_name": "string",
      "timestamp": 0,
      "message": "string",
      "image_urls": ["string"],
      "image_description": "string",
      "video_description": "string",
      "internet_response": "string",
      "link_url": "string",
      "link_description": "string"
    }
  ],
  "pagination": {
    "hasMore": true,
    "lastTimestamp": 0,
    "limit": 50
  }
}
```

Fields that are not present on a given message are omitted. To page, repeat the request with `start_after_timestamp` set to `pagination.lastTimestamp` until `hasMore` is `false`. Note that long-polling this endpoint may hit 429 and we recommend calling this when new messages have arrived. There is a 24 hour rate limit of 600 reqs/24 hrs due to the potential for this endpoint to be costly for us to serve.
  - Error: `400`, `401`, `403`, `429`, `500`
### Rewind Messages

Removes the most recent messages from a conversation (an "undo"). Useful for
discarding the last exchange before continuing.

- **URL:** `/rewind-messages`
- **Method:** POST
- **Request Body:**
```
{
  "ai_id": "string",
  "count": 1
}
```

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `ai_id` | string | one of | The AI to rewind. Mutually exclusive with `group_id`. |
| `group_id` | string | one of | The group chat to rewind. Mutually exclusive with `ai_id`. |
| `count` | number | yes | Number of most-recent messages to remove (≥ 1). |

Exactly one of `ai_id` or `group_id` must be supplied. For single-AI rewinds `ai_id`), the chat must end on an AI message both before and after the rewind, so `count` must be **even** — it removes whole user/AI exchanges. Odd counts return `400`. Group rewinds `group_id`) have no such restriction and accept any `count`.
- **Response:**
- Success: `200 OK`
  - Error: `400`, `401`, `403`, `404` (AI/group not found), `429`, `500`
### Update AI Info

Updates the persona and configuration of an **existing** AI. Only include the
fields you want to change; omitted fields are left unchanged.

- **URL:** `/update-info`
- **Method:** POST
- **Request Body:** (`ai_id` required; all others optional)
```
{
  "ai_id": "string",
  "ai_name": "string",
  "ai_gender": "string",
  "ai_backstory": "string",
  "ai_memory": "string",
  "ai_directive": "string",
  "ai_example_message": "string",
  "ai_additional_context": "string",
  "current_scene": "string",
  "user_name": "string",
  "user_gender": "string"
}
```

| Field | Type | Description |
| --- | --- | --- |
| `ai_id` | string | The AI to update. **Required.** |
| `ai_name` | string | Display name of the AI. |
| `ai_gender` | string | Gender of the AI. |
| `ai_backstory` | string | The AI's backstory. |
| `ai_memory` | string | The AI's key (long-term) memory. |
| `ai_directive` | string | Directive / response guidelines for the AI. |
| `ai_example_message` | string | Example message defining the AI's voice. |
| `ai_additional_context` | string | Additional context (availability depends on plan). |
| `current_scene` | string | The current scene / situation. |
| `user_name` | string | Your display name as seen by the AI. |
| `user_gender` | string | Your gender as seen by the AI. |

> The underlying endpoint accepts additional fields used by the Kindroid apps
(model selection, voice/call settings, beta flags, etc.). Those are
considered internal and are not part of the supported external API surface.
- **Response:**
- Success: `200 OK`
  - Error: `400`, `401`, `403`, `429`, `500`

## Group Chats

Group chats let multiple Kindroids participate in a single conversation. Groupchats can make use of rewind, get messages endpoints above. 

> **Note:** Group chats require an active subscription. Requests from
non-subscribers return `403 Forbidden`.

Create and configure groups in the Kindroid app; these endpoints operate on an
existing `group_id`. A typical turn-based loop looks like:

1. `POST /groupchats-user-message` — post the user's message.
2. `POST /groupchats-get-turn` — ask who should speak next.
3. If an `ai_id` is returned, `POST /groupchats-ai-response` for that AI, then go back to step 2. If the user's turn is returned (empty body), stop and wait for the next user message.
### Group User Message

Adds a user message to a group chat.

- **URL:** `/groupchats-user-message`
- **Method:** POST
- **Request Body:**
```
{
  "group_id": "string",
  "message": "string"
}
```

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `group_id` | string | yes | The group chat. |
| `message` | string | one of | The user's text message. Provide either `message` or `audio_url`. |
| `audio_url` | string | one of | URL of a voice message. Provide either `message` or `audio_url`. |

Exactly one of `message` or `audio_url` must be supplied.
- **Response:**
- Success: `200 OK`
  - Error: `400`, `401`, `403`, `429`, `500`
### Group Get Turn

Determines which participant should speak next in the group.

- **URL:** `/groupchats-get-turn`
- **Method:** POST
- **Request Body:**
```
{
  "group_id": "string",
  "allow_user": true
}
```

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `group_id` | string | yes | The group chat. |
| `allow_user` | boolean | yes | Whether the user is allowed to take the next turn (i.e. AI generation may end). |
- **Response:**
- Success: `200 OK` with the `ai_id` of the AI whose turn it is. An empty body indicates it is the user's turn (end of the AI generation cycle).
  - Error: `400`, `401`, `403`, `429`, `500`
### Group AI Response

Generates a response from a specific AI in the group. Pair with **Group Get
Turn** to drive the conversation.

- **URL:** `/groupchats-ai-response`
- **Method:** POST
- **Request Body:**
```
{
  "group_id": "string",
  "ai_id": "string",
  "stream": false
}
```

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `group_id` | string | yes | The group chat. |
| `ai_id` | string | yes | The AI that should respond. |
| `stream` | boolean | no | Stream the reply as it is generated. |
- **Response:**
- Success: `200 OK` with the AI's response as text. With `"stream": true`, the text is streamed as it is generated.
  - Error: `400`, `401`, `403`, `429`, `500`
### Group Chat Break

Ends the current group conversation and resets short-term memory. A greeting is
mandatory and becomes the first message in the new conversation.

- **URL:** `/groupchats-chat-break`
- **Method:** POST
- **Request Body:**
```
{
  "group_id": "string",
  "greeting": "string",
  "wipe_cascaded": false
}
```

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `group_id` | string | yes | The group chat to reset. |
| `greeting` | string | yes | First message of the new conversation. |
| wipe_cascaded | boolean | no | Also wipe the group's cascaded long-term memory built up from previous conversations. Defaults to `false` (only short-term memory is reset). |
- **Response:**
- Success: `200 OK`
  - Error: `400`, `401`, `403`, `429`, `500`
### Update Group Info

Updates the configuration of an **existing** group chat. Only include the fields
you want to change; omitted fields are left unchanged.

- **URL:** `/groupchats-update`
- **Method:** POST
- **Request Body:**
```
{
  "group_id": "string",
  "ai_list": ["string"],
  "group_name": "string",
  "group_context": "string",
  "group_directive": "string",
  "current_scene": "string"
}
```

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `group_id` | string | yes | The group chat to update. **Required.** |
| ai_list | string[] | no | The AI IDs in the group (the roster). At least one. |
| group_name | string | no | Display name of the group.     |
| group_context | string | no | Shared context / situation for the group. |
| group_directive | string | no | Directive / response guidelines for the group.  |
| current_scene | string | no | The current scene / situation.  |
- **Response:**
- Success: `200 OK`
  - Error: `400`, `401`, `403`, `429`, `500`

## Discord Bot Endpoint

Core endpoint for sending context and getting a response. Used in the
[Kindroid Discord bot](https://github.com/KindroidAI/Kindroid-discord).

- **URL:** `/discord-bot`
- **Method:** POST
- **Request Headers:**
In addition to the `Authorization` header, you should send a header
identifying the hashed, unique string of the Discord user who triggered the
call. This helps with rate limiting and preventing bot abuse. Any hashing
scheme works, but we recommend:

```
const lastUsername = conversation[conversation.length - 1].username;
// Encode username to handle non-ASCII characters, then hash to alphanumeric
const hashedUsername = Buffer.from(encodeURIComponent(lastUsername))
  .toString("base64")
  .replace(/[^a-zA-Z0-9]/g, "")
  .slice(0, 32);
```

Place it in the header as:

```
X-Kindroid-Requester: <hashedUsername>
```
- **Request Body:**
```
{
  "share_code": "string",
  "enable_filter": true,
  "conversation": [
    { "username": "string", "text": "string", "timestamp": "string" }
  ]
}
```

| Field | Type | Required | Description |
| --- | --- | --- | --- |
| `share_code` | string | yes | 5-letter share code (see the Discord repo). Must be shared by the UID who is authenticating. |
| `enable_filter` | boolean | yes | Content filter. Recommended for public servers. |
| `conversation` | array | yes | Array of `{ username, text, timestamp }`. `timestamp` is `msg.createdAt.toISOString()`. |
- **Response:**
- Success: `200 OK`
  - Error: `400`, `401`, `403`, `429`, `500`