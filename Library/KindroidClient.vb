Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Channels
Imports Kindroid.Library.Exceptions
Imports Kindroid.Library.Models
Imports Kindroid.Library.Streaming

''' <summary>
''' Client for the Kindroid AI public REST API (<c>kn_</c> API keys).
''' <para>
''' Every operation is exposed in three forms:
''' <list type="bullet">
'''   <item><description>Synchronous – blocks the calling thread.</description></item>
'''   <item><description>Async – returns a <see cref="Task"/> / <see cref="Task(Of T)"/>.</description></item>
'''   <item><description>Streaming (where supported) – returns an <see cref="IAsyncEnumerable(Of String)"/> of text chunks.</description></item>
''' </list>
''' </para>
''' </summary>
''' <example>
''' <code>
''' Dim client = New KindroidClient("kn_yourKeyHere")
'''
''' ' Synchronous
''' Dim reply = client.SendMessage("ai_abc", "Hello!")
'''
''' ' Async
''' Dim reply = Await client.SendMessageAsync("ai_abc", "Hello!")
'''
''' ' Streaming
''' Await foreach (Dim chunk In client.SendMessageStreamAsync("ai_abc", "Hello!"))
'''     Console.Write(chunk)
''' End Await
''' </code>
''' </example>
Public Class KindroidClient
    Implements IDisposable

    ' ── Constants ────────────────────────────────────────────────────────────
    Private Shared ReadOnly JsonOpts As New JsonSerializerOptions() With {
        .PropertyNameCaseInsensitive = True,
        .DefaultIgnoreCondition = Serialization.JsonIgnoreCondition.WhenWritingNull
    }

    ' ── Fields ───────────────────────────────────────────────────────────────

    Private ReadOnly _http As HttpClient
    Private ReadOnly _ownsHttp As Boolean
    Private _disposed As Boolean
    Private ReadOnly _baseUrl As String

    ' ── Constructors ─────────────────────────────────────────────────────────

    ''' <summary>
    ''' Initialises a new <see cref="KindroidClient"/> with the given API key.
    ''' An internal <see cref="HttpClient"/> is created and disposed with this instance.
    ''' </summary>
    ''' <param name="apiKey">Your <c>kn_…</c> API key.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="apiKey"/> is <see langword="Nothing"/> or empty.</exception>
    Public Sub New(apiKey As String)
        If String.IsNullOrWhiteSpace(apiKey) Then
            Throw New ArgumentNullException(NameOf(apiKey), "A Kindroid API Key is required.")
        End If

        _http = New HttpClient()
        _http.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", apiKey)
        _ownsHttp = True
        _baseUrl = "https://api.kindroid.ai/v1"
    End Sub

    ''' <summary>
    ''' Initialises a new <see cref="KindroidClient"/> using a pre-configured
    ''' <see cref="HttpClient"/>. The caller is responsible for disposing it.
    ''' </summary>
    ''' <param name="httpClient">A configured <see cref="HttpClient"/>. Must have the Authorization header set.</param>
    Public Sub New(httpClient As HttpClient)
        ArgumentNullException.ThrowIfNull(httpClient)
        _http = httpClient
        _ownsHttp = False
        _baseUrl = "https://api.kindroid.ai/v1"
    End Sub

    ' ════════════════════════════════════════════════════════════════════════
    '  PRIVATE HTTP HELPERS
    ' ════════════════════════════════════════════════════════════════════════

#Region "HTTP Helpers"

    ''' <summary>
    ''' Serialises <paramref name="body"/> as JSON and POSTs to
    ''' <c>_baseUrl + <paramref name="path"/></c>.
    ''' Throws <see cref="KindroidException"/> on non-success.
    ''' </summary>
    Private Async Function PostAsync(Of T)(path As String, body As T, cancellationToken As CancellationToken,
                                           Optional streamResponse As Boolean = False) As Task(Of HttpResponseMessage)
        Dim json = JsonSerializer.Serialize(body, JsonOpts)
        Dim content = New StringContent(json, Encoding.UTF8, "application/json")
        Dim completionOption = If(streamResponse,
            HttpCompletionOption.ResponseHeadersRead,
            HttpCompletionOption.ResponseContentRead)
        Dim response = Await _http.SendAsync(
            New HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{path}") With {.Content = content}, completionOption, cancellationToken)
        Await EnsureSuccessAsync(response, cancellationToken)
        Return response
    End Function

    ''' <summary>
    ''' GETs <c>_baseUrl + <paramref name="relativeUrl"/></c>.
    ''' Throws <see cref="KindroidException"/> on non-success.
    ''' </summary>
    Private Async Function GetAsync(relativeUrl As String,
                                    cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
        Dim response = Await _http.GetAsync($"{_baseUrl}{relativeUrl}", cancellationToken)
        Await EnsureSuccessAsync(response, cancellationToken)
        Return response
    End Function

    ''' <summary>
    ''' Reads the response body and throws a <see cref="KindroidException"/> when the
    ''' status code indicates failure.
    ''' </summary>
    Private Shared Async Function EnsureSuccessAsync(response As HttpResponseMessage, cancellationToken As CancellationToken) As Task
        If response.IsSuccessStatusCode Then Return

        Dim body As String
        Try
            body = Await response.Content.ReadAsStringAsync(cancellationToken)
        Catch
            body = String.Empty
        End Try

        Throw New KindroidException(CInt(response.StatusCode), body)
    End Function
#End Region

    ' ════════════════════════════════════════════════════════════════════════
    '  SINGLE AI ENDPOINTS
    ' ════════════════════════════════════════════════════════════════════════

#Region "Send Message"

    ''' <summary>
    ''' Sends a message to an AI and returns its full reply (synchronous).
    ''' </summary>
    ''' <param name="aiId">The AI to message.</param>
    ''' <param name="message">The user's message.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The AI's reply as a plain string.</returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Function SendMessage(aiId As String, message As String,
                                Optional cancellationToken As CancellationToken = Nothing) As String
        Return SendMessageAsync(aiId, message, cancellationToken).GetAwaiter().GetResult()
    End Function

    ''' <summary>
    ''' Sends a message to an AI and returns its full reply (async).
    ''' </summary>
    ''' <param name="aiId">The AI to message.</param>
    ''' <param name="message">The user's message.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The AI's reply as a plain string.</returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function SendMessageAsync(aiId As String, message As String,
                                           Optional cancellationToken As CancellationToken = Nothing) As Task(Of String)
        Dim req As New SendMessageRequest() With {
            .AiId = aiId,
            .Message = message,
            .Stream = False
        }
        Using response = Await PostAsync("/send-message", req, cancellationToken, streamResponse:=False)
            Return Await response.Content.ReadAsStringAsync(cancellationToken)
        End Using
    End Function

    ''' <summary>
    ''' Sends a message to an AI and streams the reply as an async sequence of text chunks.
    ''' </summary>
    ''' <param name="aiId">The AI to message.</param>
    ''' <param name="message">The user's message.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>An async sequence of text chunks.</returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Function SendMessageStreamAsync(aiId As String,
                                       message As String,
                                       Optional cancellationToken As CancellationToken = Nothing) As IAsyncEnumerable(Of String)

        ' 1. HAPUS kata kunci "Async Iterator" pada deklarasi fungsi di atas.
        ' 2. Buat Channel untuk menjembatani background streaming ke return value.
        Dim ch = Channel.CreateUnbounded(Of String)(New UnboundedChannelOptions With {
        .SingleWriter = True,
        .SingleReader = True
    })

        ' 3. Jalankan proses POST dan pembacaan stream di dalam background Task.
        Task.Run(Async Function()
                     Dim response As HttpResponseMessage = Nothing
                     Try
                         Dim req As New SendMessageRequest() With {.AiId = aiId, .Message = message, .Stream = True}

                         ' Melakukan HTTP Post secara async
                         response = Await PostAsync("/send-message", req, cancellationToken, streamResponse:=True)

                         ' Ambil enumerator dari StreamingHelper.ReadChunksAsync (hasil konversi sebelumnya)
                         Dim asyncSequence = StreamingHelper.ReadChunksAsync(response, cancellationToken)
                         Dim enumerator = asyncSequence.GetAsyncEnumerator(cancellationToken)

                         Try
                             ' VB.NET manual loop sebagai pengganti "Await Foreach" C#
                             While Await enumerator.MoveNextAsync()
                                 Dim chunk = enumerator.Current
                                 ' Kirim chunk teks masuk ke dalam Channel
                                 Await ch.Writer.WriteAsync(chunk, cancellationToken)
                             End While
                         Finally
                             enumerator.DisposeAsync().AsTask().Wait()
                         End Try

                     Catch ex As Exception
                         ' Jika ada error (misal KindroidException atau putus koneksi), teruskan ke konsumen
                         ch.Writer.TryComplete(ex)
                         Return
                     Finally
                         ' Pastikan HttpResponseMessage selalu di-dispose
                         If response IsNot Nothing Then response.Dispose()
                     End Try

                     ' Menandakan data stream dari server sudah habis dan sukses sepenuhnya
                     ch.Writer.Complete()
                 End Function, cancellationToken)

        ' 4. Kembalikan data stream yang bisa langsung dibaca oleh pemanggil method ini
        Return ch.Reader.ReadAllAsync(cancellationToken)
    End Function

#End Region

#Region "Chat Break"

    ''' <summary>
    ''' Ends the current chat and resets the AI's short-term memory (synchronous).
    ''' </summary>
    ''' <param name="aiId">The AI to reset.</param>
    ''' <param name="greeting">First message of the new conversation.</param>
    ''' <param name="wipeCascaded">Also wipe cascaded long-term memory.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Sub ChatBreak(aiId As String,
                         greeting As String,
                         Optional wipeCascaded As Boolean = False,
                         Optional cancellationToken As CancellationToken = Nothing)
        ChatBreakAsync(aiId, greeting, wipeCascaded, cancellationToken).GetAwaiter().GetResult()
    End Sub

    ''' <summary>
    ''' Ends the current chat and resets the AI's short-term memory (async).
    ''' </summary>
    ''' <param name="aiId">The AI to reset.</param>
    ''' <param name="greeting">First message of the new conversation.</param>
    ''' <param name="wipeCascaded">Also wipe cascaded long-term memory.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function ChatBreakAsync(aiId As String,
                                        greeting As String,
                                        Optional wipeCascaded As Boolean = False,
                                        Optional cancellationToken As CancellationToken = Nothing) As Task
        Dim req As New ChatBreakRequest() With {
            .AiId = aiId,
            .Greeting = greeting,
            .WipeCascaded = wipeCascaded
        }
        Using response = Await PostAsync("/chat-break", req, cancellationToken, streamResponse:=False)
            ' No content expected, just ensure success or throw
        End Using
    End Function
#End Region

#Region "Get Chat Messages"
    ''' <summary>
    ''' Retrieves a page of chat history for a Kindroid (synchronous).
    ''' </summary>
    ''' <param name="aiId">The AI whose history to fetch.</param>
    ''' <param name="limit">Page size, 1–100. Defaults to 50.</param>
    ''' <param name="startAfterTimestamp">Cursor from the previous page's <c>LastTimestamp</c>.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The response containing messages and pagination info.</returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Function GetChatMessages(aiId As String,
                                    Optional limit As Integer? = Nothing,
                                    Optional startAfterTimestamp As Long? = Nothing,
                                    Optional cancellationToken As CancellationToken = Nothing) As GetChatMessagesResponse
        Return GetChatMessagesAsync(aiId, limit:=limit, startAfterTimestamp:=startAfterTimestamp, cancellationToken:=cancellationToken).GetAwaiter().GetResult()
    End Function

    ''' <summary>
    ''' Retrieves a page of chat history for a Kindroid (async).
    ''' </summary>
    ''' <param name="aiId">The AI whose history to fetch.</param>
    ''' <param name="limit">Page size, 1–100. Defaults to 50.</param>
    ''' <param name="startAfterTimestamp">Cursor from the previous page's <c>LastTimestamp</c>.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The response containing messages and pagination info.</returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function GetChatMessagesAsync(aiId As String,
                                               Optional limit As Integer? = Nothing,
                                               Optional startAfterTimestamp As Long? = Nothing,
                                               Optional cancellationToken As CancellationToken = Nothing) As Task(Of GetChatMessagesResponse)
        Return Await GetChatMessagesInternalAsync(aiId:=aiId, groupId:=Nothing, limit:=limit, startAfterTimestamp:=startAfterTimestamp, cancellationToken:=cancellationToken)
    End Function

    ''' <summary>
    ''' Retrieves a page of chat history for a group chat (synchronous).
    ''' </summary>
    ''' <param name="groupId">The group chat whose history to fetch.</param>
    ''' <param name="limit">Page size, 1–100. Defaults to 50.</param>
    ''' <param name="startAfterTimestamp">Cursor from the previous page's <c>LastTimestamp</c>.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The response containing messages and pagination info.</returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Function GetGroupChatMessages(groupId As String,
                                         Optional limit? As Integer = Nothing,
                                         Optional startAfterTimestamp As Long? = Nothing,
                                         Optional cancellationToken As CancellationToken = Nothing) As GetChatMessagesResponse
        Return GetGroupChatMessagesAsync(groupId, limit:=limit, startAfterTimestamp:=startAfterTimestamp, cancellationToken:=cancellationToken).GetAwaiter().GetResult()
    End Function

    ''' <summary>
    ''' Retrieves a page of chat history for a group chat (async).
    ''' </summary>
    ''' <param name="groupId">The group chat whose history to fetch.</param>
    ''' <param name="limit">Page size, 1–100. Defaults to 50.</param>
    ''' <param name="startAfterTimestamp">Cursor from the previous page's <c>LastTimestamp</c>.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The response containing messages and pagination info.</returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function GetGroupChatMessagesAsync(groupId As String,
                                              Optional limit As Integer? = Nothing,
                                              Optional startAfterTimestamp As Long? = Nothing,
                                              Optional cancellationToken As CancellationToken = Nothing) As Task(Of GetChatMessagesResponse)
        Return Await GetChatMessagesInternalAsync(aiId:=Nothing, groupId:=groupId, limit:=limit, startAfterTimestamp:=startAfterTimestamp, cancellationToken:=cancellationToken)
    End Function

    Private Async Function GetChatMessagesInternalAsync(aiId As String, groupId As String, limit As Integer?,
                                                        startAfterTimestamp As Long?, cancellationToken As CancellationToken) As Task(Of GetChatMessagesResponse)
        Dim qs As New StringBuilder("/get-chat-messages?")

        If aiId IsNot Nothing Then
            qs.Append($"ai_id={Uri.EscapeDataString(aiId)}&")
        ElseIf groupId IsNot Nothing Then
            qs.Append($"group_id={Uri.EscapeDataString(groupId)}&")
        Else
            ArgumentNullException.ThrowIfNullOrWhiteSpace(aiId)
            ArgumentNullException.ThrowIfNullOrWhiteSpace(groupId)
        End If

        If limit.HasValue Then qs.Append($"limit={limit.Value}&")
        If startAfterTimestamp.HasValue Then qs.Append($"start_after_timestamp={startAfterTimestamp.Value}&")

        Dim url = qs.ToString().TrimEnd("&"c)
        Using response = Await GetAsync(url, cancellationToken)
            Dim json = Await response.Content.ReadAsStringAsync(cancellationToken)
            Return JsonSerializer.Deserialize(Of GetChatMessagesResponse)(json, JsonOpts)
        End Using
    End Function
#End Region

#Region "Rewind Messages"
    ''' <summary>
    ''' Removes the most-recent messages from a single-AI conversation (synchronous).
    ''' <para>
    ''' For single-AI rewinds <paramref name="count"/> must be even (removes whole user/AI exchanges).
    ''' </para>
    ''' </summary>
    ''' <param name="aiId">The AI to rewind.</param>
    ''' <param name="count">Number of most-recent messages to remove (≥ 1, must be even for AI chats).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Sub RewindMessages(aiId As String,
                              count As Integer,
                              Optional cancellationToken As CancellationToken = Nothing)
        RewindMessagesAsync(aiId, count, cancellationToken).GetAwaiter().GetResult()
    End Sub

    ''' <summary>
    ''' Removes the most-recent messages from a single-AI conversation (async).
    ''' <para>
    ''' For single-AI rewinds <paramref name="count"/> must be even (removes whole user/AI exchanges).
    ''' </para>
    ''' </summary>
    ''' <param name="aiId">The AI to rewind.</param>
    ''' <param name="count">Number of most-recent messages to remove (≥ 1, must be even for AI chats).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function RewindMessagesAsync(aiId As String,
                                              count As Integer,
                                              Optional cancellationToken As CancellationToken = Nothing) As Task
        Dim req As New RewindMessagesRequest() With {
            .AiId = aiId,
            .Count = count
        }

        Using Await PostAsync("/rewind-messages", req, cancellationToken)
            ' No need to put anything here
        End Using
    End Function

    ''' <summary>
    ''' Removes the most-recent messages from a group chat (synchronous).
    ''' </summary>
    ''' <param name="groupId">The group chat to rewind.</param>
    ''' <param name="count">Number of most-recent messages to remove (≥ 1).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Sub RewindGroupMessages(groupId As String,
                                   count As Integer,
                                   Optional cancellationToken As CancellationToken = Nothing)
        RewindGroupMessagesAsync(groupId, count, cancellationToken).GetAwaiter().GetResult()
    End Sub

    ''' <summary>
    ''' Removes the most-recent messages from a group chat (asynchronous).
    ''' </summary>
    ''' <param name="groupId">The group chat to rewind.</param>
    ''' <param name="count">Number of most-recent messages to remove (≥ 1).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function RewindGroupMessagesAsync(groupId As String,
                                                   count As Integer,
                                                   Optional cancellationToken As CancellationToken = Nothing) As Task
        Dim req As New RewindMessagesRequest() With {.GroupId = groupId, .Count = count}
        Using Await PostAsync("/rewind-messages", req, cancellationToken)
            ' No need to put anything here
        End Using
    End Function
#End Region

#Region "Update AI Info"

    ''' <summary>
    ''' Updates the persona and configuration of an existing AI (synchronous).
    ''' Only populate the fields you want to change; all others are left unchanged.
    ''' </summary>
    ''' <param name="request">The update request. <see cref="UpdateAiInfoRequest.AiId"/> is required.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Sub UpdateAiInfo(request As UpdateAiInfoRequest,
                            Optional cancellationToken As CancellationToken = Nothing)
        UpdateAiInfoAsync(request, cancellationToken).GetAwaiter().GetResult()
    End Sub

    ''' <summary>
    ''' Updates the persona and configuration of an existing AI (asynchronous).
    ''' Only populate the fields you want to change; all others are left unchanged.
    ''' </summary>
    ''' <param name="request">The update request. <see cref="UpdateAiInfoRequest.AiId"/> is required.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function UpdateAiInfoAsync(request As UpdateAiInfoRequest,
                            Optional cancellationToken As CancellationToken = Nothing) As Task
        ArgumentNullException.ThrowIfNull(request)
        Using Await PostAsync("/update-info", request, cancellationToken)
            ' Nyenyenye
        End Using
    End Function
#End Region

    ' ════════════════════════════════════════════════════════════════════════
    '  GROUP CHAT ENDPOINTS
    ' ════════════════════════════════════════════════════════════════════════

#Region "Group User Message"

    ''' <summary>
    ''' Adds a text message from the user to a group chat (synchronous).
    ''' </summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="message">The user's text message.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Sub GroupUserMessage(groupId As String,
                                    message As String,
                                    Optional cancellationToken As CancellationToken = Nothing)
        GroupUserMessageAsync(groupId, message, cancellationToken).GetAwaiter().GetResult()
    End Sub

    ''' <summary>
    ''' Adds a text message from the user to a group chat (asynchronous).
    ''' </summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="message">The user's text message.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function GroupUserMessageAsync(groupId As String,
                                                    message As String,
                                                    Optional cancellationToken As CancellationToken = Nothing) As Task
        Dim req As New GroupUserMessageRequest() With {.GroupId = groupId, .Message = message}
        Using Await PostAsync("/groupchats-user-message", req, cancellationToken)
            'huehuehue
        End Using
    End Function

    ''' <summary>
    ''' Adds a voice message (by URL) from the user to a group chat (synchronous).
    ''' </summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="audioUrl">URL of the voice message.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Sub GroupUserAudioMessage(groupId As String,
                                          audioUrl As String,
                                          Optional cancellationToken As CancellationToken = Nothing)
        GroupUserAudioMessageAsync(groupId, audioUrl, cancellationToken).GetAwaiter().GetResult()
    End Sub

    ''' <summary>
    ''' Adds a voice message (by URL) from the user to a group chat (asynchronous).
    ''' </summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="audioUrl">URL of the voice message.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function GroupUserAudioMessageAsync(groupId As String,
                                                          audioUrl As String,
                                                          Optional cancellationToken As CancellationToken = Nothing) As Task
        Dim req As New GroupUserMessageRequest() With {.GroupId = groupId, .AudioUrl = audioUrl}
        Using Await PostAsync("/groupchats-user-message", req, cancellationToken)
            'huehuehue
        End Using
    End Function

#End Region

#Region "Group Get Turn"

    ''' <summary>
    ''' Determines which participant should speak next in the group (synchronous).
    ''' </summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="allowUser">Whether the user is allowed to be the next speaker.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>
    ''' A <see cref="GroupGetTurnResult"/> whose <see cref="GroupGetTurnResult.AiId"/> is the
    ''' AI that should speak, or <see langword="Nothing"/> when it is the user's turn.
    ''' </returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Function GroupGetTurn(groupId As String,
                                     Optional allowUser As Boolean = True,
                                     Optional cancellationToken As CancellationToken = Nothing) As GroupGetTurnResult
        Return GroupGetTurnAsync(groupId, allowUser, cancellationToken).GetAwaiter().GetResult()
    End Function

    ''' <summary>Determines which participant should speak next in the group (async).</summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="allowUser">Whether the user is allowed to be the next speaker.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>
    ''' A <see cref="GroupGetTurnResult"/> whose <see cref="GroupGetTurnResult.AiId"/> is the
    ''' AI that should speak, or <see langword="Nothing"/> when it is the user's turn.
    ''' </returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function GroupGetTurnAsync(groupId As String,
                                                Optional allowUser As Boolean = True,
                                                Optional cancellationToken As CancellationToken = Nothing) As Task(Of GroupGetTurnResult)
        Dim req As New GroupGetTurnRequest() With {.GroupId = groupId, .AllowUser = allowUser}
        Using response = Await PostAsync("/groupchats-get-turn", req, cancellationToken)
            Dim body = (Await response.Content.ReadAsStringAsync(cancellationToken)).Trim()
            Return New GroupGetTurnResult(If(String.IsNullOrEmpty(body), Nothing, body))
        End Using
    End Function

#End Region

#Region "Group AI Response"

    ''' <summary>
    ''' Generates a response from a specific AI in the group and returns the full reply (synchronous).
    ''' </summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="aiId">The AI that should respond.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The AI's reply as a plain string.</returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Function GroupAiResponse(groupId As String,
                                         aiId As String,
                                         Optional cancellationToken As CancellationToken = Nothing) As String
        Return GroupAiResponseAsync(groupId, aiId, cancellationToken).GetAwaiter().GetResult()
    End Function

    ''' <summary>Generates a response from a specific AI in the group (async).</summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="aiId">The AI that should respond.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The AI's reply as a plain string.</returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function GroupAiResponseAsync(groupId As String,
                                                    aiId As String,
                                                    Optional cancellationToken As CancellationToken = Nothing) As Task(Of String)
        Dim req As New GroupAiResponseRequest() With {.GroupId = groupId, .AiId = aiId, .Stream = False}
        Using response = Await PostAsync("/groupchats-ai-response", req, cancellationToken, streamResponse:=False)
            Return Await response.Content.ReadAsStringAsync(cancellationToken)
        End Using
    End Function

    ''' <summary>
    ''' Generates a response from a specific AI in the group and streams the reply (async).
    ''' </summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="aiId">The AI that should respond.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>An async sequence of text chunks.</returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Function GroupAiResponseStreamAsync(groupId As String,
                                                                   aiId As String,
                                                                   Optional cancellationToken As CancellationToken = Nothing) As IAsyncEnumerable(Of String)
        Dim ch = Channel.CreateUnbounded(Of String)(New UnboundedChannelOptions With {.SingleReader = True, .SingleWriter = True})

        Task.Run(Async Function()
                     Dim response As HttpResponseMessage = Nothing
                     Try
                         Dim req As New GroupAiResponseRequest() With {.GroupId = groupId, .AiId = aiId, .Stream = True}

                         ' Await pertama: Request HTTP Post ke API Kindroid
                         response = Await PostAsync("/groupchats-ai-response", req, cancellationToken, streamResponse:=True)

                         ' Ambil stream generator dari helper yang sudah kita buat sebelumnya
                         Dim asyncSequence = StreamingHelper.ReadChunksAsync(response, cancellationToken)
                         Dim enumerator = asyncSequence.GetAsyncEnumerator(cancellationToken)

                         ' Pengganti "Await foreach": Kita sebut "Manual Enumeration Loop"
                         While Await enumerator.MoveNextAsync()
                             Dim chunk = enumerator.Current

                             ' Masukkan chunk teks yang didapat ke dalam pipa Channel
                             Await ch.Writer.WriteAsync(chunk, cancellationToken)
                         End While
                         ' Wajib bersihkan enumerator stream setelah loop selesai
                         Await enumerator.DisposeAsync()
                     Catch ex As Exception
                         ' Jika di tengah jalan API error/RTO, kirim error-nya ke pembaca loop
                         ch.Writer.TryComplete(ex)
                         Return
                     Finally
                         ' Pastikan response HTTP selalu ditutup agar tidak bocor memory
                         If response IsNot Nothing Then response.Dispose()
                     End Try

                     ' Informasikan ke Channel bahwa streaming dari server sudah selesai sukses
                     ch.Writer.Complete()
                 End Function, cancellationToken)

        ' 4. Langsung kembalikan ujung pipa Channel. Pembaca di luar otomatis bisa membaca data secara streaming.
        Return ch.Reader.ReadAllAsync(cancellationToken)
    End Function

#End Region

#Region "Group Chat Break"

    ''' <summary>
    ''' Ends the current group conversation and resets short-term memory (synchronous).
    ''' </summary>
    ''' <param name="groupId">The group chat to reset.</param>
    ''' <param name="greeting">First message of the new conversation.</param>
    ''' <param name="wipeCascaded">Also wipe cascaded long-term memory.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Sub GroupChatBreak(groupId As String,
                                   greeting As String,
                                   Optional wipeCascaded As Boolean = False,
                                   Optional cancellationToken As CancellationToken = Nothing)
        GroupChatBreakAsync(groupId, greeting, wipeCascaded, cancellationToken).GetAwaiter().GetResult()
    End Sub

    ''' <summary>Ends the current group conversation and resets short-term memory (async).</summary>
    Public Async Function GroupChatBreakAsync(groupId As String,
                                                   greeting As String,
                                                   Optional wipeCascaded As Boolean = False,
                                                   Optional cancellationToken As CancellationToken = Nothing) As Task
        Dim req As New GroupChatBreakRequest() With {
                .GroupId = groupId, .Greeting = greeting, .WipeCascaded = wipeCascaded
            }
        Using Await PostAsync("/groupchats-chat-break", req, cancellationToken)
            ' huehuehue
        End Using
    End Function

#End Region

#Region "Update Group Info"

    ''' <summary>
    ''' Updates the configuration of an existing group chat (synchronous).
    ''' Only populate the fields you want to change; all others are left unchanged.
    ''' </summary>
    ''' <param name="request">The update request. <see cref="UpdateGroupInfoRequest.GroupId"/> is required.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Sub UpdateGroupInfo(request As UpdateGroupInfoRequest,
                                    Optional cancellationToken As CancellationToken = Nothing)
        UpdateGroupInfoAsync(request, cancellationToken).GetAwaiter().GetResult()
    End Sub

    ''' <summary>Updates the configuration of an existing group chat (async).</summary>
    Public Async Function UpdateGroupInfoAsync(request As UpdateGroupInfoRequest,
                                                    Optional cancellationToken As CancellationToken = Nothing) As Task
        ArgumentNullException.ThrowIfNull(request)
        Using Await PostAsync("/groupchats-update", request, cancellationToken)
            ' huehuehue
        End Using
    End Function

#End Region

    ' ════════════════════════════════════════════════════════════════════════
    '  IDisposable
    ' ════════════════════════════════════════════════════════════════════════

    ''' <inheritdoc/>
    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        If _ownsHttp Then _http.Dispose()
        GC.SuppressFinalize(Me)
    End Sub

End Class
