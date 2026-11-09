Imports Kindroid.Library
Imports System.Diagnostics
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports Microsoft.Extensions.Configuration

''' <summary>
''' Non-interactive probes that hit the live Kindroid API with a RAW HttpClient
''' (deliberately NOT through Kindroid.Library) so we observe real server behaviour
''' independent of any library bug. Output is plain text so it can be redirected to a file.
''' The API key is never printed.
''' </summary>
Friend Module Probe

    Private Const BaseUrl As String = "https://api.kindroid.ai/v1"

    Private _http As HttpClient
    Private _aiId As String
    Private _groupId As String

    Friend Async Function RunAsync(args As String(), aiId As String, groupId As String) As Task
        Console.OutputEncoding = Encoding.UTF8

        Dim config As IConfiguration = New ConfigurationBuilder().AddUserSecrets("27cceab5-9467-42a3-beae-5713e765703f").Build()
        Dim apiKey = config("Kindroid:API_KEY")
        If String.IsNullOrWhiteSpace(apiKey) Then
            Console.WriteLine("No Kindroid:API_KEY in user-secrets.")
            Return
        End If

        _aiId = aiId
        _groupId = groupId
        _http = New HttpClient() With {.Timeout = TimeSpan.FromMinutes(5)}
        _http.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", apiKey)

        ' args(0) = "probe", args(1) = scenario, rest = scenario args
        Dim scenario = If(args.Length > 1, args(1).ToLowerInvariant(), "help")
        Dim rest = args.Skip(2).ToArray()

        Select Case scenario
            Case "auth" : Await AuthAsync()
            Case "history" : Await HistoryAsync()
            Case "send" : Await SendAsync(If(rest.Length > 0, rest(0), "Halo, balas singkat aja ya."))
            Case "stream" : Await StreamAsync(If(rest.Length > 0, rest(0), "Ceritain satu paragraf pendek tentang hujan."))
            Case "badid" : Await BadIdAsync()
            Case "sendedge" : Await SendEdgeAsync()
            Case "rewind" : Await RewindAsync()
            Case "history2" : Await History2Async(If(rest.Length > 0, rest(0), _aiId), If(rest.Length > 1, CInt(rest(1)), 12))
            Case "lib" : Await LibraryAsync()
            Case "group" : Await GroupAsync()
            Case "update" : Await UpdateAsync()
            Case "chatbreak" : Await ChatBreakAsync()
            Case "ratelimit" : Await RateLimitAsync(If(rest.Length > 0, CInt(rest(0)), 20), If(rest.Length > 1, CInt(rest(1)), 10))
            Case "raw" : Await RawCommandAsync(rest)
            Case Else
                Console.WriteLine("Scenarios: auth | history | send [msg] | stream [msg] | badid | sendedge | rewind | raw <METHOD> <path> [json]")
        End Select
    End Function

    ' ── Low-level helpers ──────────────────────────────────────────────────────

    Private Sub Header(title As String)
        Console.WriteLine()
        Console.WriteLine($"=== {title} ===")
    End Sub

    Private Function Show(s As String, Optional max As Integer = 1500) As String
        If s Is Nothing Then Return "<null>"
        Dim t = If(s.Length > max, s.Substring(0, max) & $"...(+{s.Length - max} chars)", s)
        Return t.Replace(vbCr, "\r").Replace(vbLf, "\n")
    End Function

    ''' <summary>One request, fully buffered. Prints status, relevant headers, elapsed, body.</summary>
    Private Async Function CallAsync(label As String, method As HttpMethod, path As String, Optional jsonBody As String = Nothing,
                                     Optional client As HttpClient = Nothing) As Task(Of (Status As Integer, Body As String))
        Dim http = If(client, _http)
        Await Task.Delay(3000) ' pace requests: the server 429s bursts (~12 req in a few seconds)
        Using req As New HttpRequestMessage(method, BaseUrl & path)
            If jsonBody IsNot Nothing Then req.Content = New StringContent(jsonBody, Encoding.UTF8, "application/json")
            Dim sw = Stopwatch.StartNew()
            Try
                Using resp = Await http.SendAsync(req, HttpCompletionOption.ResponseContentRead)
                    Dim body = Await resp.Content.ReadAsStringAsync()
                    sw.Stop()
                    Dim ct = resp.Content.Headers.ContentType?.ToString()
                    Dim extra As New List(Of String)
                    For Each hn In {"Retry-After", "X-RateLimit-Remaining", "X-RateLimit-Limit", "X-RateLimit-Reset", "RateLimit-Remaining"}
                        Dim vals As IEnumerable(Of String) = Nothing
                        If resp.Headers.TryGetValues(hn, vals) Then extra.Add($"{hn}={String.Join(",", vals)}")
                    Next
                    Console.WriteLine($"[{label}] {method} {path}{If(jsonBody IsNot Nothing, " body=" & Show(jsonBody, 300), "")}")
                    Console.WriteLine($"   -> {CInt(resp.StatusCode)} {resp.ReasonPhrase} | {sw.ElapsedMilliseconds} ms | content-type={ct} | len={body.Length} {String.Join(" ", extra)}")
                    Console.WriteLine($"   body: {Show(body)}")
                    Return (CInt(resp.StatusCode), body)
                End Using
            Catch ex As Exception
                sw.Stop()
                Console.WriteLine($"[{label}] {method} {path} -> EXCEPTION {ex.GetType().Name}: {ex.Message} ({sw.ElapsedMilliseconds} ms)")
                Return (-1, ex.Message)
            End Try
        End Using
    End Function

    Private Function Post(path As String, obj As Object) As String
        Return JsonSerializer.Serialize(obj)
    End Function

    ' ── Scenarios ──────────────────────────────────────────────────────────────

    Private Async Function AuthAsync() As Task
        Header("AUTH: bad / missing key")
        Using bad As New HttpClient()
            bad.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", "kn_definitely_not_a_real_key")
            Await CallAsync("badkey", HttpMethod.Get, $"/get-chat-messages?ai_id={_aiId}&limit=1", client:=bad)
            Await CallAsync("badkey-post", HttpMethod.Post, "/send-message", JsonSerializer.Serialize(New With {.ai_id = _aiId, .message = "hi"}), bad)
        End Using
        Using none As New HttpClient()
            Await CallAsync("nokey", HttpMethod.Get, $"/get-chat-messages?ai_id={_aiId}&limit=1", client:=none)
        End Using
    End Function

    Private Async Function HistoryAsync() As Task
        Header("HISTORY (budget: 600 req / 24h - keep it small)")
        Dim r1 = Await CallAsync("default", HttpMethod.Get, $"/get-chat-messages?ai_id={_aiId}")
        Dim r2 = Await CallAsync("limit=2", HttpMethod.Get, $"/get-chat-messages?ai_id={_aiId}&limit=2")

        ' paging with cursor
        If r2.Status = 200 Then
            Using doc = JsonDocument.Parse(r2.Body)
                Dim p = doc.RootElement.GetProperty("pagination")
                Console.WriteLine($"   pagination: {p.GetRawText()}")
                Dim last = p.GetProperty("lastTimestamp").GetRawText()
                Await CallAsync("page2", HttpMethod.Get, $"/get-chat-messages?ai_id={_aiId}&limit=2&start_after_timestamp={last}")
            End Using
        End If

        Await CallAsync("limit=0", HttpMethod.Get, $"/get-chat-messages?ai_id={_aiId}&limit=0")
        Await CallAsync("limit=101", HttpMethod.Get, $"/get-chat-messages?ai_id={_aiId}&limit=101")
        Await CallAsync("limit=abc", HttpMethod.Get, $"/get-chat-messages?ai_id={_aiId}&limit=abc")
        Await CallAsync("both-ids", HttpMethod.Get, $"/get-chat-messages?ai_id={_aiId}&group_id={_groupId}&limit=1")
        Await CallAsync("neither", HttpMethod.Get, "/get-chat-messages?limit=1")
        Await CallAsync("group", HttpMethod.Get, $"/get-chat-messages?group_id={_groupId}&limit=3")
    End Function

    Private Async Function SendAsync(msg As String) As Task
        Header("SEND-MESSAGE (non-stream)")
        Await CallAsync("send", HttpMethod.Post, "/send-message", JsonSerializer.Serialize(New With {.ai_id = _aiId, .message = msg}))
    End Function

    Private Async Function StreamAsync(msg As String) As Task
        Header("SEND-MESSAGE (stream=true) raw chunk timing")
        Dim body = JsonSerializer.Serialize(New With {.ai_id = _aiId, .message = msg, .stream = True})
        Using req As New HttpRequestMessage(HttpMethod.Post, BaseUrl & "/send-message") With {.Content = New StringContent(body, Encoding.UTF8, "application/json")}
            Dim sw = Stopwatch.StartNew()
            Using resp = Await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead)
                Console.WriteLine($"   headers after {sw.ElapsedMilliseconds} ms: {CInt(resp.StatusCode)} {resp.ReasonPhrase}")
                Console.WriteLine($"   content-type={resp.Content.Headers.ContentType} transfer-encoding={String.Join(",", resp.Headers.TransferEncoding)} content-length={resp.Content.Headers.ContentLength}")
                Using stream = Await resp.Content.ReadAsStreamAsync()
                    Dim buf(1023) As Byte
                    Dim n As Integer
                    Dim i = 0
                    Dim all As New List(Of Byte)
                    n = Await stream.ReadAsync(buf)
                    While n > 0
                        i += 1
                        all.AddRange(buf.Take(n))
                        Dim text = Encoding.UTF8.GetString(buf, 0, n)
                        Console.WriteLine($"   chunk#{i} +{sw.ElapsedMilliseconds} ms bytes={n} whitespaceOnly={String.IsNullOrWhiteSpace(text)} text={Show(text, 200)}")
                        n = Await stream.ReadAsync(buf)
                    End While
                    Console.WriteLine($"   total chunks={i} bytes={all.Count} elapsed={sw.ElapsedMilliseconds} ms")
                    Console.WriteLine($"   full: {Show(Encoding.UTF8.GetString(all.ToArray()), 4000)}")
                End Using
            End Using
        End Using
    End Function

    Private Async Function BadIdAsync() As Task
        Header("BAD IDS")
        Await CallAsync("send-badai", HttpMethod.Post, "/send-message", JsonSerializer.Serialize(New With {.ai_id = "nope_nope", .message = "hi"}))
        Await CallAsync("send-noai", HttpMethod.Post, "/send-message", JsonSerializer.Serialize(New With {.message = "hi"}))
        Await CallAsync("send-nomsg", HttpMethod.Post, "/send-message", JsonSerializer.Serialize(New With {.ai_id = _aiId}))
        Await CallAsync("history-badai", HttpMethod.Get, "/get-chat-messages?ai_id=nope_nope&limit=1")
        Await CallAsync("rewind-badai", HttpMethod.Post, "/rewind-messages", JsonSerializer.Serialize(New With {.ai_id = "nope_nope", .count = 2}))
        Await CallAsync("group-badgroup-turn", HttpMethod.Post, "/groupchats-get-turn", JsonSerializer.Serialize(New With {.group_id = "nope_nope", .allow_user = True}))
        Await CallAsync("bad-json", HttpMethod.Post, "/send-message", "{not json")
        Await CallAsync("unknown-path", HttpMethod.Get, "/does-not-exist")
    End Function

    Private Async Function SendEdgeAsync() As Task
        Header("SEND EDGE CASES (each may cost an AI reply)")
        Await CallAsync("empty-message", HttpMethod.Post, "/send-message", JsonSerializer.Serialize(New With {.ai_id = _aiId, .message = ""}))
        Await CallAsync("whitespace-message", HttpMethod.Post, "/send-message", JsonSerializer.Serialize(New With {.ai_id = _aiId, .message = "   "}))
        Await CallAsync("unicode", HttpMethod.Post, "/send-message", JsonSerializer.Serialize(New With {.ai_id = _aiId, .message = "你好 😊 *waves* **hello** [bracket] <tag>"}))
    End Function

    Private Async Function RewindAsync() As Task
        Header("REWIND (single AI)")
        Await CallAsync("count=1(odd)", HttpMethod.Post, "/rewind-messages", JsonSerializer.Serialize(New With {.ai_id = _aiId, .count = 1}))
        Await CallAsync("count=0", HttpMethod.Post, "/rewind-messages", JsonSerializer.Serialize(New With {.ai_id = _aiId, .count = 0}))
        Await CallAsync("count=-2", HttpMethod.Post, "/rewind-messages", JsonSerializer.Serialize(New With {.ai_id = _aiId, .count = -2}))
        Await CallAsync("both-ids", HttpMethod.Post, "/rewind-messages", JsonSerializer.Serialize(New With {.ai_id = _aiId, .group_id = _groupId, .count = 2}))
        Await CallAsync("neither", HttpMethod.Post, "/rewind-messages", JsonSerializer.Serialize(New With {.count = 2}))
    End Function

    Private Async Function GroupAsync() As Task
        Header("GROUP")
        Dim gid = _groupId
        Await CallAsync("user-msg-neither", HttpMethod.Post, "/groupchats-user-message", JsonSerializer.Serialize(New With {.group_id = gid}))
        Await CallAsync("user-msg-both", HttpMethod.Post, "/groupchats-user-message", JsonSerializer.Serialize(New With {.group_id = gid, .message = "x", .audio_url = "https://example.com/a.mp3"}))
        Await CallAsync("user-msg-bad-audio", HttpMethod.Post, "/groupchats-user-message", JsonSerializer.Serialize(New With {.group_id = gid, .audio_url = "https://example.invalid/a.mp3"}))
        Await CallAsync("user-msg", HttpMethod.Post, "/groupchats-user-message", JsonSerializer.Serialize(New With {.group_id = gid, .message = "Halo semuanya, singkat aja ya."}))
        Await CallAsync("turn-no-allow_user", HttpMethod.Post, "/groupchats-get-turn", JsonSerializer.Serialize(New With {.group_id = gid}))

        For i = 1 To 6
            Dim t = Await CallAsync($"turn#{i}", HttpMethod.Post, "/groupchats-get-turn", JsonSerializer.Serialize(New With {.group_id = gid, .allow_user = True}))
            Dim ai = t.Body.Trim().Trim(""""c)
            If t.Status <> 200 OrElse ai.Length = 0 Then
                Console.WriteLine("   (user turn / stop)")
                Exit For
            End If
            Console.WriteLine($"   raw turn body bytes: [{String.Join(" ", Encoding.UTF8.GetBytes(t.Body).Select(Function(b) b.ToString("X2")))}]")
            If i = 1 Then
                ' stream the first AI reply, use sync for the rest
                Await StreamGroupAsync(gid, ai)
            Else
                Await CallAsync($"ai-response#{i}", HttpMethod.Post, "/groupchats-ai-response", JsonSerializer.Serialize(New With {.group_id = gid, .ai_id = ai, .stream = False}))
            End If
        Next

        Await CallAsync("turn-allow_user=false", HttpMethod.Post, "/groupchats-get-turn", JsonSerializer.Serialize(New With {.group_id = gid, .allow_user = False}))
        Await CallAsync("ai-response-not-in-roster", HttpMethod.Post, "/groupchats-ai-response", JsonSerializer.Serialize(New With {.group_id = gid, .ai_id = "nope_nope"}))
        Await CallAsync("ai-response-single-ai-id", HttpMethod.Post, "/groupchats-ai-response", JsonSerializer.Serialize(New With {.group_id = gid, .ai_id = _aiId}))
        Await CallAsync("history", HttpMethod.Get, $"/get-chat-messages?group_id={gid}&limit=10")
    End Function

    Private Async Function StreamGroupAsync(gid As String, ai As String) As Task
        Dim body = JsonSerializer.Serialize(New With {.group_id = gid, .ai_id = ai, .stream = True})
        Using req As New HttpRequestMessage(HttpMethod.Post, BaseUrl & "/groupchats-ai-response") With {.Content = New StringContent(body, Encoding.UTF8, "application/json")}
            Dim sw = Stopwatch.StartNew()
            Using resp = Await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead)
                Console.WriteLine($"[group-stream] -> {CInt(resp.StatusCode)} ct={resp.Content.Headers.ContentType} ttfb={sw.ElapsedMilliseconds} ms")
                Using stream = Await resp.Content.ReadAsStreamAsync()
                    Dim buf(1023) As Byte
                    Dim n = Await stream.ReadAsync(buf)
                    Dim chunks = 0
                    Dim all As New List(Of Byte)
                    Dim firstChunks As New List(Of String)
                    While n > 0
                        chunks += 1
                        all.AddRange(buf.Take(n))
                        If chunks <= 4 Then firstChunks.Add(Show(Encoding.UTF8.GetString(buf, 0, n), 60))
                        n = Await stream.ReadAsync(buf)
                    End While
                    Console.WriteLine($"   chunks={chunks} bytes={all.Count} total={sw.ElapsedMilliseconds} ms first4={String.Join(" | ", firstChunks)}")
                    Console.WriteLine($"   full: {Show(Encoding.UTF8.GetString(all.ToArray()), 2000)}")
                End Using
            End Using
        End Using
    End Function

    Private Async Function UpdateAsync() As Task
        Header("UPDATE-INFO / GROUPCHATS-UPDATE (test AI+group, sets then restores scene)")
        Await CallAsync("update-only-id", HttpMethod.Post, "/update-info", JsonSerializer.Serialize(New With {.ai_id = _aiId}))
        Await CallAsync("update-scene", HttpMethod.Post, "/update-info", JsonSerializer.Serialize(New With {.ai_id = _aiId, .current_scene = "PROBE scene: a quiet rainy cafe."}))
        Await CallAsync("update-empty-string", HttpMethod.Post, "/update-info", JsonSerializer.Serialize(New With {.ai_id = _aiId, .current_scene = ""}))
        Await CallAsync("update-unknown-field", HttpMethod.Post, "/update-info", JsonSerializer.Serialize(New With {.ai_id = _aiId, .bogus_field = "x"}))
        Await CallAsync("update-badai", HttpMethod.Post, "/update-info", JsonSerializer.Serialize(New With {.ai_id = "nope_nope", .current_scene = "x"}))

        Await CallAsync("g-update-only-id", HttpMethod.Post, "/groupchats-update", JsonSerializer.Serialize(New With {.group_id = _groupId}))
        Await CallAsync("g-update-scene", HttpMethod.Post, "/groupchats-update", JsonSerializer.Serialize(New With {.group_id = _groupId, .current_scene = "PROBE group scene"}))
        Await CallAsync("g-update-empty-ailist", HttpMethod.Post, "/groupchats-update", "{""group_id"":""" & _groupId & """,""ai_list"":[]}")
        Await CallAsync("g-update-unknown-ai", HttpMethod.Post, "/groupchats-update", "{""group_id"":""" & _groupId & """,""ai_list"":[""nope_nope""]}")
        Await CallAsync("g-update-badgroup", HttpMethod.Post, "/groupchats-update", JsonSerializer.Serialize(New With {.group_id = "nope_nope", .current_scene = "x"}))
        Await CallAsync("g-update-clear-scene", HttpMethod.Post, "/groupchats-update", JsonSerializer.Serialize(New With {.group_id = _groupId, .current_scene = ""}))
    End Function

    Private Async Function ChatBreakAsync() As Task
        Header("CHAT-BREAK (destructive; test AI/group only)")
        Await CallAsync("cb-no-greeting", HttpMethod.Post, "/chat-break", JsonSerializer.Serialize(New With {.ai_id = _aiId}))
        Await CallAsync("cb-empty-greeting", HttpMethod.Post, "/chat-break", JsonSerializer.Serialize(New With {.ai_id = _aiId, .greeting = ""}))
        Await CallAsync("cb-badai", HttpMethod.Post, "/chat-break", JsonSerializer.Serialize(New With {.ai_id = "nope_nope", .greeting = "hi"}))
        Await CallAsync("cb", HttpMethod.Post, "/chat-break", JsonSerializer.Serialize(New With {.ai_id = _aiId, .greeting = "PROBE greeting 你好"}))
        Await CallAsync("cb-history", HttpMethod.Get, $"/get-chat-messages?ai_id={_aiId}&limit=5")
        Await CallAsync("cb-wipe", HttpMethod.Post, "/chat-break", JsonSerializer.Serialize(New With {.ai_id = _aiId, .greeting = "PROBE greeting after wipe", .wipe_cascaded = True}))
        Await CallAsync("gcb-no-greeting", HttpMethod.Post, "/groupchats-chat-break", JsonSerializer.Serialize(New With {.group_id = _groupId}))
        Await CallAsync("gcb-badgroup", HttpMethod.Post, "/groupchats-chat-break", JsonSerializer.Serialize(New With {.group_id = "nope_nope", .greeting = "hi"}))
        Await CallAsync("gcb", HttpMethod.Post, "/groupchats-chat-break", JsonSerializer.Serialize(New With {.group_id = _groupId, .greeting = "PROBE group greeting"}))
        Await CallAsync("gcb-history", HttpMethod.Get, $"/get-chat-messages?group_id={_groupId}&limit=5")
    End Function

    ''' <summary>Exercises the real Kindroid.Library client against the live server (paced).</summary>
    Private Async Function LibraryAsync() As Task
        Header("LIBRARY (KindroidClient) live verification")
        Dim key = _http.DefaultRequestHeaders.Authorization.Parameter
        Using client As New Kindroid.Library.KindroidClient(key)
            Dim step1 = Async Function(name As String, work As Func(Of Task)) As Task
                            Await Task.Delay(3000)
                            Try
                                Await work()
                                Console.WriteLine($"[OK ] {name}")
                            Catch ex As Exception
                                Console.WriteLine($"[EXC] {name}: {ex.GetType().Name}: {Show(ex.Message, 300)}")
                            End Try
                        End Function

            Await step1("history default (models populated?)", Async Function()
                                                                   Dim r = Await client.GetChatMessagesAsync(_aiId, 2)
                                                                   Console.WriteLine($"     count={r.Messages.Count} hasMore={r.Pagination.HasMore} last={r.Pagination.LastTimestamp} limit={r.Pagination.Limit}")
                                                                   For Each m In r.Messages
                                                                       Console.WriteLine($"     {m.Sender} user={m.IsFromUser} ai={m.IsFromAi} at={m.SentAt:u} imgs={m.ImageUrls.Count} name='{m.DisplayName}' text={Show(m.Message, 40)}")
                                                                   Next
                                                               End Function)
            Await step1("history past end (lastTimestamp null)", Async Function()
                                                                     Dim r = Await client.GetChatMessagesAsync(_aiId, 1, 9007199254740991L)
                                                                     Console.WriteLine($"     count={r.Messages.Count} last={If(r.Pagination.LastTimestamp.HasValue, "value", "Nothing")}")
                                                                 End Function)
            Await step1("group history (image_urls null)", Async Function()
                                                               Dim r = Await client.GetGroupChatMessagesAsync(_groupId, 3)
                                                               Console.WriteLine($"     count={r.Messages.Count} firstImgs={r.Messages(0).ImageUrls.Count} name={r.Messages(0).DisplayName}")
                                                           End Function)
            Await step1("enumerate all single history", Async Function()
                                                            Dim n = 0
                                                            Await client.EnumerateChatMessagesAsync(_aiId, 2).ForEachAsync(Sub(m) n += 1)
                                                            Console.WriteLine($"     total={n}")
                                                        End Function)
            Await step1("send (sync API)", Async Function()
                                               Dim r = Await client.SendMessageAsync(_aiId, "Halo lagi, satu kalimat aja.")
                                               Console.WriteLine($"     reply={Show(r, 80)}")
                                           End Function)
            Await step1("stream (first chunks incl whitespace, concat == text)", Async Function()
                                                                                     Dim sb As New StringBuilder()
                                                                                     Dim first As New List(Of String)
                                                                                     Await client.SendMessageStreamAsync(_aiId, "Satu kalimat pendek tentang pagi.").ForEachAsync(
                                                                                         Sub(c)
                                                                                             If first.Count < 3 Then first.Add($"'{c}'")
                                                                                             sb.Append(c)
                                                                                         End Sub)
                                                                                     Console.WriteLine($"     first={String.Join(",", first)} len={sb.Length} startsWithSpace={sb.ToString().StartsWith(" ")} text={Show(sb.ToString(), 60)}")
                                                                                 End Function)
            Await step1("stream bad ai (error must surface, not hang)", Async Function()
                                                                            Await client.SendMessageStreamAsync("nope_nope", "hi").ReadAllTextAsync()
                                                                        End Function)
            Await step1("stream cancelled early (dispose mid-stream)", Async Function()
                                                                           Dim e = client.SendMessageStreamAsync(_aiId, "Ceritakan cerita panjang tentang laut.").GetAsyncEnumerator()
                                                                           Await e.MoveNextAsync()
                                                                           Await e.MoveNextAsync()
                                                                           Await e.DisposeAsync()
                                                                           Console.WriteLine("     disposed after 2 chunks without hanging")
                                                                       End Function)
            Await step1("typed exception: bad key -> 401", Async Function()
                                                               Using bad As New Kindroid.Library.KindroidClient("kn_bad")
                                                                   Await bad.SendMessageAsync(_aiId, "hi")
                                                               End Using
                                                           End Function)
            Await step1("typed exception: rewind unknown ai -> 404", Async Function()
                                                                         Await client.RewindMessagesAsync("nope_nope", 2)
                                                                     End Function)
            Await step1("local validation: odd rewind", Async Function()
                                                            Await client.RewindMessagesAsync(_aiId, 1)
                                                        End Function)
            Await step1("local validation: limit 101", Async Function()
                                                           Await client.GetChatMessagesAsync(_aiId, 101)
                                                       End Function)
            Await step1("group: user msg + run turns", Async Function()
                                                           Await client.GroupUserMessageAsync(_groupId, "Selamat siang semuanya.")
                                                           Await Task.Delay(3000)
                                                           Await client.GroupRunTurnsAsync(_groupId, True, 4).ForEachAsync(Sub(r) Console.WriteLine($"     {r.AiId}: {Show(r.Text, 60)}"))
                                                       End Function)
            Await step1("group stream", Async Function()
                                            Dim t = Await client.GroupAiResponseStreamAsync(_groupId, "JVyWoik2KN1SSzgrZl0t").ReadAllTextAsync()
                                            Console.WriteLine($"     len={t.Length} text={Show(t, 60)}")
                                        End Function)
            Await step1("group rewind 1 (odd allowed)", Async Function()
                                                            Await client.RewindGroupMessagesAsync(_groupId, 1)
                                                        End Function)
            Await step1("update-info set/clear scene", Async Function()
                                                           Dim req As New Kindroid.Library.Models.UpdateAiInfoRequest() With {.AiId = _aiId, .CurrentScene = "lib scene"}
                                                           Await client.UpdateAiInfoAsync(req)
                                                       End Function)
            Await step1("hash requester", Async Function()
                                              Console.WriteLine($"     {Kindroid.Library.KindroidClient.HashRequester("Alice!#1 你好")}")
                                              Await Task.CompletedTask
                                          End Function)
        End Using
    End Function

    ''' <summary>
    ''' Read-only structural survey of a long-running AI's history. Prints statistics only, never message text.
    ''' Pages with limit=100, paced, capped at maxPages requests.
    ''' </summary>
    Private Async Function History2Async(aiId As String, maxPages As Integer) As Task
        Header($"HISTORY SURVEY (read-only, limit=100, max {maxPages} pages)")
        Dim cursor As String = Nothing
        Dim pages = 0, total = 0, dup = 0, outOfOrder = 0
        Dim ids As New HashSet(Of String)
        Dim fields As New SortedDictionary(Of String, Integer)
        Dim senders As New SortedDictionary(Of String, Integer)
        Dim nonEmptyImages = 0, nullImages = 0
        Dim firstTs As Long = 0, lastTs As Long = 0, prevTs As Long = Long.MinValue
        Dim hasMore = True
        Dim lastCursorNull = False
        Dim maxLen = 0, libFirstPageOk = "n/a"

        While hasMore AndAlso pages < maxPages
            Await Task.Delay(3000)
            Dim path = $"/get-chat-messages?ai_id={Uri.EscapeDataString(aiId)}&limit=100" & If(cursor IsNot Nothing, "&start_after_timestamp=" & cursor, "")
            Using resp = Await _http.GetAsync(BaseUrl & path)
                Dim body = Await resp.Content.ReadAsStringAsync()
                If Not resp.IsSuccessStatusCode Then
                    Console.WriteLine($"page {pages + 1}: HTTP {CInt(resp.StatusCode)} {Show(body, 100)}")
                    Exit While
                End If
                pages += 1
                Using doc = JsonDocument.Parse(body)
                    Dim msgs = doc.RootElement.GetProperty("messages")
                    For Each m In msgs.EnumerateArray()
                        total += 1
                        For Each p In m.EnumerateObject()
                            fields(p.Name) = If(fields.ContainsKey(p.Name), fields(p.Name) + 1, 1)
                        Next
                        If Not (m.EnumerateObject().All(Function(q) q.Name = "id" OrElse q.Name = "sender" OrElse q.Name = "display_name" OrElse q.Name = "timestamp" OrElse q.Name = "message" OrElse (q.Name = "image_urls" AndAlso q.Value.ValueKind = JsonValueKind.Null))) Then
                            Console.WriteLine($"  [rich msg {m.GetProperty("id").GetString()} sender={m.GetProperty("sender").GetString()}]")
                            For Each q In m.EnumerateObject()
                                If q.Name = "id" OrElse q.Name = "sender" OrElse q.Name = "timestamp" Then Continue For
                                Console.WriteLine($"     {q.Name} ({q.Value.ValueKind}): {Show(q.Value.GetRawText(), 160)}")
                            Next
                        End If
                        Dim id = m.GetProperty("id").GetString()
                        If Not ids.Add(id) Then dup += 1
                        Dim s = m.GetProperty("sender").GetString()
                        senders(s) = If(senders.ContainsKey(s), senders(s) + 1, 1)
                        Dim ts = m.GetProperty("timestamp").GetInt64()
                        If firstTs = 0 Then firstTs = ts
                        lastTs = ts
                        If ts < prevTs Then outOfOrder += 1
                        prevTs = ts
                        Dim img As JsonElement
                        If m.TryGetProperty("image_urls", img) Then
                            If img.ValueKind = JsonValueKind.Null Then
                                nullImages += 1
                            ElseIf img.GetArrayLength() > 0 Then
                                nonEmptyImages += 1
                            End If
                        End If
                        Dim txt As JsonElement
                        If m.TryGetProperty("message", txt) AndAlso txt.ValueKind = JsonValueKind.String Then maxLen = Math.Max(maxLen, txt.GetString().Length)
                    Next
                    Dim pg = doc.RootElement.GetProperty("pagination")
                    hasMore = pg.GetProperty("hasMore").GetBoolean()
                    Dim lt = pg.GetProperty("lastTimestamp")
                    lastCursorNull = lt.ValueKind = JsonValueKind.Null
                    cursor = If(lastCursorNull, Nothing, lt.GetRawText())
                    Console.WriteLine($"page {pages}: {msgs.GetArrayLength()} msgs hasMore={hasMore} lastTs={lt.GetRawText()}")

                    ' Cross-check: the library must parse this same first page identically
                    If pages = 1 Then
                        Dim parsed = JsonSerializer.Deserialize(Of Kindroid.Library.Models.GetChatMessagesResponse)(body)
                        libFirstPageOk = $"raw={msgs.GetArrayLength()} lib={parsed.Messages.Count} libWithImages={parsed.Messages.Where(Function(x) x.ImageUrls.Count > 0).Count()} libSenderTypeNonEmpty={parsed.Messages.Where(Function(x) x.SenderType <> "").Count()}"
                    End If
                End Using
            End Using
        End While

        Console.WriteLine()
        Console.WriteLine($"pages={pages} totalMessages={total} duplicateIds={dup} outOfOrder={outOfOrder} reachedEnd={Not hasMore}")
        Console.WriteLine($"range: {DateTimeOffset.FromUnixTimeMilliseconds(firstTs):u} -> {DateTimeOffset.FromUnixTimeMilliseconds(lastTs):u}")
        Console.WriteLine("senders: " & String.Join(", ", senders.Select(Function(kv) $"{kv.Key}={kv.Value}")))
        Console.WriteLine("fields (name=count): " & String.Join(", ", fields.Select(Function(kv) $"{kv.Key}={kv.Value}")))
        Console.WriteLine($"image_urls: null={nullImages} nonEmpty={nonEmptyImages}; longest message={maxLen} chars")
        Console.WriteLine($"library parse of page 1: {libFirstPageOk}")
    End Function

    ''' <summary>Polls a cheap endpoint every N seconds until the 429 clears, to measure the cooldown window.</summary>
    Private Async Function RateLimitAsync(intervalSeconds As Integer, maxMinutes As Integer) As Task
        Header($"RATE LIMIT cooldown poll every {intervalSeconds}s (max {maxMinutes} min)")
        Dim sw = Stopwatch.StartNew()
        Dim first = True
        While sw.Elapsed < TimeSpan.FromMinutes(maxMinutes)
            Using req As New HttpRequestMessage(HttpMethod.Get, BaseUrl & $"/get-chat-messages?ai_id={_aiId}&limit=1")
                Using resp = Await _http.SendAsync(req)
                    Dim body = Await resp.Content.ReadAsStringAsync()
                    Console.WriteLine($"t+{sw.Elapsed.TotalSeconds:F0}s -> {CInt(resp.StatusCode)} {Show(body, 80)}")
                    If first OrElse resp.IsSuccessStatusCode Then
                        For Each h In resp.Headers
                            Console.WriteLine($"     hdr {h.Key}: {String.Join(",", h.Value)}")
                        Next
                        first = False
                    End If
                    If resp.IsSuccessStatusCode Then
                        Console.WriteLine($"   cleared after ~{sw.Elapsed.TotalSeconds:F0}s")
                        Return
                    End If
                End Using
            End Using
            Await Task.Delay(TimeSpan.FromSeconds(intervalSeconds))
        End While
        Console.WriteLine("   still limited at end of poll window")
    End Function

    Private Async Function RawCommandAsync(rest As String()) As Task
        If rest.Length < 2 Then
            Console.WriteLine("usage: probe raw <GET|POST> <path-with-query> [json-body]")
            Return
        End If
        Await CallAsync("raw", New HttpMethod(rest(0).ToUpperInvariant()), rest(1), If(rest.Length > 2, rest(2), Nothing))
    End Function

End Module
