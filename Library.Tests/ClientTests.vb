Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports Kindroid.Library
Imports Kindroid.Library.Exceptions
Imports Kindroid.Library.Models
Imports Xunit

Public Class ClientTests

    ' ── Errors ──────────────────────────────────────────────────────────────

    <Theory>
    <InlineData(400, GetType(KindroidException))>
    <InlineData(401, GetType(KindroidAuthenticationException))>
    <InlineData(403, GetType(KindroidForbiddenException))>
    <InlineData(404, GetType(KindroidNotFoundException))>
    <InlineData(429, GetType(KindroidRateLimitException))>
    <InlineData(500, GetType(KindroidException))>
    Public Async Function Non_success_throws_typed_exception_with_body(status As Integer, expected As Type) As Task
        Dim h As New FakeHandler(Function(r, b) Text(CType(status, HttpStatusCode), "AI not found"))
        Using c = Make(h)
            Dim ex = Await Assert.ThrowsAnyAsync(Of KindroidException)(Function() c.SendMessageAsync("ai", "hi"))
            Assert.IsType(expected, ex)
            Assert.Equal(status, ex.StatusCode)
            Assert.Equal("AI not found", ex.ResponseBody)
            Assert.Contains("AI not found", ex.Message, StringComparison.Ordinal)
        End Using
    End Function

    ' ── Requests ────────────────────────────────────────────────────────────

    <Fact>
    Public Async Function SendMessage_sends_bearer_and_json_and_returns_text() As Task
        Dim h As New FakeHandler(Function(r, b) Text(HttpStatusCode.OK, " hello 你好"))
        Using c = Make(h)
            Dim reply = Await c.SendMessageAsync("ai_1", "hi")
            Assert.Equal(" hello 你好", reply) ' not trimmed
            Dim sent = h.Requests.Single()
            Assert.Equal("Bearer", sent.Request.Headers.Authorization.Scheme)
            Assert.Equal("kn_test", sent.Request.Headers.Authorization.Parameter)
            Assert.Equal("https://api.kindroid.ai/v1/send-message", sent.Request.RequestUri.AbsoluteUri)
            Using doc = JsonDocument.Parse(sent.Body)
                Assert.Equal("ai_1", doc.RootElement.GetProperty("ai_id").GetString())
                Assert.Equal("hi", doc.RootElement.GetProperty("message").GetString())
                Assert.False(doc.RootElement.GetProperty("stream").GetBoolean())
            End Using
        End Using
    End Function

    <Fact>
    Public Async Function Group_rewind_sends_group_id_only() As Task
        Dim h As New FakeHandler(Function(r, b) Text(HttpStatusCode.OK, "OK"))
        Using c = Make(h)
            Await c.RewindGroupMessagesAsync("g1", 3)
            Using doc = JsonDocument.Parse(h.Requests.Single().Body)
                Dim ignored As JsonElement
                Assert.False(doc.RootElement.TryGetProperty("ai_id", ignored))
                Assert.Equal("g1", doc.RootElement.GetProperty("group_id").GetString())
                Assert.Equal(3, doc.RootElement.GetProperty("count").GetInt32())
            End Using
        End Using
    End Function

    <Fact>
    Public Async Function Update_omits_null_fields_and_keeps_empty_strings() As Task
        Dim h As New FakeHandler(Function(r, b) Text(HttpStatusCode.OK, "OK"))
        Using c = Make(h)
            Await c.UpdateAiInfoAsync(New UpdateAiInfoRequest() With {.AiId = "a", .CurrentScene = ""})
            Using doc = JsonDocument.Parse(h.Requests.Single().Body)
                Dim ignored As JsonElement
                Assert.Equal("", doc.RootElement.GetProperty("current_scene").GetString())
                Assert.False(doc.RootElement.TryGetProperty("ai_name", ignored))
            End Using
        End Using
    End Function

    ' ── Validation (nothing may be sent) ────────────────────────────────────

    <Fact>
    Public Async Function Local_validation_rejects_before_sending() As Task
        Dim h As New FakeHandler(Function(r, b) Text(HttpStatusCode.OK, "OK"))
        Using c = Make(h)
            Await Assert.ThrowsAsync(Of ArgumentException)(Function() c.SendMessageAsync("ai", ""))
            Await Assert.ThrowsAsync(Of ArgumentException)(Function() c.SendMessageAsync(" ", "x"))
            Await Assert.ThrowsAsync(Of ArgumentOutOfRangeException)(Function() c.RewindMessagesAsync("ai", 1))
            Await Assert.ThrowsAsync(Of ArgumentOutOfRangeException)(Function() c.RewindMessagesAsync("ai", 0))
            Await Assert.ThrowsAsync(Of ArgumentOutOfRangeException)(Function() c.RewindGroupMessagesAsync("g", 0))
            Await Assert.ThrowsAsync(Of ArgumentOutOfRangeException)(Function() c.GetChatMessagesAsync("ai", 0))
            Await Assert.ThrowsAsync(Of ArgumentOutOfRangeException)(Function() c.GetChatMessagesAsync("ai", 101))
            Await Assert.ThrowsAsync(Of ArgumentException)(Function() c.ChatBreakAsync("ai", ""))
            Dim empty As New UpdateGroupInfoRequest() With {.GroupId = "g", .AiList = New Collections.ObjectModel.Collection(Of String)()}
            Await Assert.ThrowsAsync(Of ArgumentException)(Function() c.UpdateGroupInfoAsync(empty))
            Assert.Throws(Of ArgumentException)(Function() c.SendMessageStreamAsync("ai", ""))
            Assert.Empty(h.Requests)
        End Using
    End Function

    ' ── Retry ───────────────────────────────────────────────────────────────

    <Fact>
    Public Async Function Rate_limit_is_retried_when_configured() As Task
        Dim calls = 0
        Dim h As New FakeHandler(Function(r, b)
                                     calls += 1
                                     Return If(calls < 3, Text(CType(429, HttpStatusCode), "Too Many Requests (API limit)"), Text(HttpStatusCode.OK, "ok"))
                                 End Function)
        Using c = Make(h, New KindroidClientOptions() With {.MaxRetries = 2, .RetryDelay = TimeSpan.Zero})
            Assert.Equal("ok", Await c.SendMessageAsync("ai", "hi"))
            Assert.Equal(3, calls)
        End Using
    End Function

    <Fact>
    Public Async Function Rate_limit_is_not_retried_by_default() As Task
        Dim calls = 0
        Dim h As New FakeHandler(Function(r, b)
                                     calls += 1
                                     Return Text(CType(429, HttpStatusCode), "x")
                                 End Function)
        Using c = Make(h)
            Await Assert.ThrowsAsync(Of KindroidRateLimitException)(Function() c.SendMessageAsync("ai", "hi"))
            Assert.Equal(1, calls)
        End Using
    End Function

    ' ── History ─────────────────────────────────────────────────────────────

    Private Const Page1 As String = "{""messages"":[{""id"":""1"",""sender"":""user"",""display_name"":""Al"",""timestamp"":1000,""image_urls"":null,""message"":""a""},{""id"":""2"",""sender"":""ai"",""timestamp"":2000,""message"":""b""}],""pagination"":{""hasMore"":true,""lastTimestamp"":2000,""limit"":2}}"
    Private Const Page2 As String = "{""messages"":[{""id"":""3"",""sender"":""user"",""timestamp"":3000,""message"":""c""}],""pagination"":{""hasMore"":false,""lastTimestamp"":3000,""limit"":2}}"
    Private Const EmptyPage As String = "{""messages"":[],""pagination"":{""hasMore"":false,""lastTimestamp"":null,""limit"":2}}"

    <Fact>
    Public Async Function History_parses_nulls_and_omitted_fields() As Task
        Dim h As New FakeHandler(Function(r, b) Text(HttpStatusCode.OK, Page1))
        Using c = Make(h)
            Dim r = Await c.GetChatMessagesAsync("ai", 2)
            Assert.Equal(2, r.Messages.Count)
            Assert.True(r.Messages(0).IsFromUser)
            Assert.Empty(r.Messages(0).ImageUrls) ' JSON null -> empty
            Assert.True(r.Messages(1).IsFromAi)
            Assert.Equal(String.Empty, r.Messages(1).DisplayName)
            Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(2000), r.Messages(1).SentAt)
            Assert.Equal(2000L, r.Pagination.LastTimestamp)
            Assert.True(r.Pagination.HasMore)
        End Using
    End Function

    <Fact>
    Public Async Function History_parses_attachment_fields_observed_live() As Task
        Const json As String = "{""messages"":[" &
            "{""id"":""1"",""sender"":""user"",""image_urls"":[""!enc:U2FsdGVkX1+abc""],""message"":""img"",""image_description"":""(Attached image: a cat)""}," &
            "{""id"":""2"",""sender"":""user"",""image_urls"":null,""message"":""vid"",""video_description"":""A person walks.""}," &
            "{""id"":""3"",""sender"":""user"",""image_urls"":null,""message"":""link"",""link_url"":""https://example.com/"",""link_description"":""(Attached link)""}," &
            "{""id"":""4"",""sender"":""user"",""image_urls"":null,""message"":""doc"",""file_name"":""TUGAS 1.docx""}," &
            "{""id"":""5"",""sender"":""user"",""image_urls"":null,""message"":""web"",""internet_response"":""(Context from internet: x)""}" &
            "],""pagination"":{""hasMore"":false,""lastTimestamp"":5,""limit"":50}}"
        Dim h As New FakeHandler(Function(r, b) Text(HttpStatusCode.OK, json))
        Using c = Make(h)
            Dim m = (Await c.GetChatMessagesAsync("ai")).Messages
            Assert.Equal("!enc:U2FsdGVkX1+abc", m(0).ImageUrls.Single())
            Assert.Contains("a cat", m(0).ImageDescription, StringComparison.Ordinal)
            Assert.Equal("A person walks.", m(1).VideoDescription)
            Assert.Equal("https://example.com/", m(2).LinkUrl)
            Assert.Equal("(Attached link)", m(2).LinkDescription)
            Assert.Equal("TUGAS 1.docx", m(3).FileName)
            Assert.StartsWith("(Context from internet", m(4).InternetResponse, StringComparison.Ordinal)
        End Using
    End Function

    <Fact>
    Public Async Function History_empty_page_has_null_cursor() As Task
        Dim h As New FakeHandler(Function(r, b) Text(HttpStatusCode.OK, EmptyPage))
        Using c = Make(h)
            Dim r = Await c.GetChatMessagesAsync("ai")
            Assert.Empty(r.Messages)
            Assert.Null(r.Pagination.LastTimestamp)
        End Using
    End Function

    <Fact>
    Public Async Function Enumerate_follows_cursor_until_no_more() As Task
        Dim h As New FakeHandler(Function(r, b) Text(HttpStatusCode.OK, If(r.RequestUri.Query.Contains("start_after_timestamp=2000", StringComparison.Ordinal), Page2, Page1)))
        Using c = Make(h)
            Dim ids As New List(Of String)
            Await c.EnumerateChatMessagesAsync("ai", 2).ForEachAsync(Sub(m) ids.Add(m.Id))
            Assert.Equal({"1", "2", "3"}, ids)
            Assert.Equal(2, h.Requests.Count)
            Assert.Contains("ai_id=ai", h.Requests(0).Request.RequestUri.Query, StringComparison.Ordinal)
        End Using
    End Function

    ' ── Streaming ───────────────────────────────────────────────────────────

    Private Shared Function StreamResponse(chunks As IEnumerable(Of String)) As HttpResponseMessage
        Dim r As New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StreamContent(New ChunkedStream(chunks))}
        r.Content.Headers.ContentType = New Headers.MediaTypeHeaderValue("text/event-stream")
        Return r
    End Function

    <Fact>
    Public Async Function Stream_keeps_whitespace_only_chunks_verbatim() As Task
        Dim h As New FakeHandler(Function(r, b) StreamResponse({" ", "(*", "萧", "衍", "*)", vbLf, " ", "end"}))
        Using c = Make(h)
            Dim chunks As New List(Of String)
            Await c.SendMessageStreamAsync("ai", "hi").ForEachAsync(Sub(x) chunks.Add(x))
            Assert.Equal(" ", chunks(0))
            Assert.Equal(" (*萧衍*)" & vbLf & " end", String.Concat(chunks))
            Using doc = JsonDocument.Parse(h.Requests.Single().Body)
                Assert.True(doc.RootElement.GetProperty("stream").GetBoolean())
            End Using
        End Using
    End Function

    <Fact>
    Public Async Function Stream_reassembles_multibyte_characters_split_across_reads() As Task
        Dim bytes = Encoding.UTF8.GetBytes("萧衍")
        Dim h As New FakeHandler(Function(r, b)
                                     Dim parts As IEnumerable(Of Byte()) = {bytes.Take(2).ToArray(), bytes.Skip(2).Take(3).ToArray(), bytes.Skip(5).ToArray()}
                                     Return New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StreamContent(New ChunkedStream(parts))}
                                 End Function)
        Using c = Make(h)
            Assert.Equal("萧衍", Await c.SendMessageStreamAsync("ai", "hi").ReadAllTextAsync())
        End Using
    End Function

    <Fact>
    Public Async Function Stream_http_error_is_thrown_from_enumeration_not_hung() As Task
        Dim h As New FakeHandler(Function(r, b) Text(HttpStatusCode.BadRequest, "AI not found"))
        Using c = Make(h)
            Dim seq = c.SendMessageStreamAsync("ai", "hi") ' lazy: nothing sent yet
            Assert.Empty(h.Requests)
            Dim work = seq.ReadAllTextAsync()
            Dim done = Await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(10)))
            Assert.Same(work, done)
            Dim ex = Await Assert.ThrowsAsync(Of KindroidException)(Function() work)
            Assert.Equal(400, ex.StatusCode)
        End Using
    End Function

    <Fact>
    Public Async Function Stream_network_failure_is_forwarded() As Task
        Dim h As New FakeHandler(Function(r, b) As HttpResponseMessage
                                     Throw New HttpRequestException("boom")
                                 End Function)
        Using c = Make(h)
            Dim work = c.SendMessageStreamAsync("ai", "hi").ReadAllTextAsync()
            Dim done = Await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(10)))
            Assert.Same(work, done)
            Await Assert.ThrowsAsync(Of HttpRequestException)(Function() work)
        End Using
    End Function

    <Fact>
    Public Async Function Stream_early_dispose_does_not_hang() As Task
        Dim h As New FakeHandler(Function(r, b) StreamResponse(Enumerable.Repeat("x", 1000)))
        Using c = Make(h)
            Dim e = c.SendMessageStreamAsync("ai", "hi").GetAsyncEnumerator()
            Assert.True(Await e.MoveNextAsync())
            Dim disposeTask = e.DisposeAsync().AsTask()
            Dim done = Await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(10)))
            Assert.Same(disposeTask, done)
        End Using
    End Function

    ' ── Group ───────────────────────────────────────────────────────────────

    <Fact>
    Public Async Function GetTurn_empty_body_means_user_turn() As Task
        Dim h As New FakeHandler(Function(r, b) Text(HttpStatusCode.OK, ""))
        Using c = Make(h)
            Dim t = Await c.GroupGetTurnAsync("g")
            Assert.True(t.IsUserTurn)
            Assert.Null(t.AiId)
        End Using
    End Function

    <Fact>
    Public Async Function RunTurns_loops_until_user_turn() As Task
        Dim turns As New Queue(Of String)({"aiA", "aiB", ""})
        Dim h As New FakeHandler(Function(r, b)
                                     If r.RequestUri.AbsolutePath.EndsWith("get-turn", StringComparison.Ordinal) Then Return Text(HttpStatusCode.OK, turns.Dequeue())
                                     Using doc = JsonDocument.Parse(b)
                                         Return Text(HttpStatusCode.OK, "reply from " & doc.RootElement.GetProperty("ai_id").GetString())
                                     End Using
                                 End Function)
        Using c = Make(h)
            Dim replies As New List(Of GroupAiReply)
            Await c.GroupRunTurnsAsync("g").ForEachAsync(Sub(x) replies.Add(x))
            Assert.Equal({"aiA", "aiB"}, replies.Select(Function(x) x.AiId))
            Assert.Equal("reply from aiB", replies(1).Text)
        End Using
    End Function

    ' ── Misc ────────────────────────────────────────────────────────────────

    <Theory>
    <InlineData("Alice", "QWxpY2U")>
    <InlineData("", "")>
    Public Sub HashRequester_matches_documented_scheme(user As String, expected As String)
        Assert.Equal(expected, KindroidClient.HashRequester(user))
    End Sub

    <Fact>
    Public Sub HashRequester_is_alphanumeric_and_at_most_32_chars()
        Dim h = KindroidClient.HashRequester("很长的用户名 with spaces & symbols !*'() " & New String("z"c, 100))
        Assert.True(h.Length <= 32)
        Assert.Matches("^[a-zA-Z0-9]*$", h)
    End Sub

    <Fact>
    Public Async Function Disposed_client_throws() As Task
        Dim h As New FakeHandler(Function(r, b) Text(HttpStatusCode.OK, "x"))
        Dim c = Make(h)
        c.Dispose()
        Await Assert.ThrowsAsync(Of ObjectDisposedException)(Function() c.SendMessageAsync("ai", "hi"))
    End Function

End Class
