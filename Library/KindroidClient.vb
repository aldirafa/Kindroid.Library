Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Runtime.CompilerServices
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Threading
Imports Kindroid.Library.Exceptions
Imports Kindroid.Library.Models
Imports Kindroid.Library.Streaming

''' <summary>
''' Client for the Kindroid AI public REST API (<c>kn_</c> API keys).
''' <para>
''' Every operation is exposed in up to three forms:
''' <list type="bullet">
'''   <item><description>Synchronous: blocks the calling thread.</description></item>
'''   <item><description>Async: returns a <see cref="Task"/> / <see cref="Task(Of T)"/>.</description></item>
'''   <item><description>Streaming (reply-generating calls only): returns an <see cref="IAsyncEnumerable(Of String)"/> of text chunks.</description></item>
''' </list>
''' </para>
''' <para>
''' Failures are reported with a <see cref="KindroidException"/> (or one of its subclasses). Invalid arguments are
''' rejected locally with <see cref="ArgumentException"/> before any request is sent, which also saves rate-limit budget.
''' </para>
''' <para>
''' <b>Rate limiting.</b> The server limits requests per API key and answers with <c>429</c> (no <c>Retry-After</c> header);
''' bursts of a dozen requests within a few seconds were enough to trigger it, and every endpoint is affected while it lasts.
''' <c>get-chat-messages</c> additionally allows only 600 requests per 24 hours. See <see cref="KindroidClientOptions.MaxRetries"/>.
''' </para>
''' </summary>
''' <example>
''' <code language="vb">
''' Using client As New KindroidClient("kn_yourKeyHere")
'''     ' Synchronous
'''     Dim reply = client.SendMessage("ai_abc", "Hello!")
'''
'''     ' Async
'''     Dim reply2 = Await client.SendMessageAsync("ai_abc", "Hello!")
'''
'''     ' Streaming (VB has no "Await For Each", so enumerate manually)
'''     Dim e = client.SendMessageStreamAsync("ai_abc", "Hello!").GetAsyncEnumerator()
'''     Try
'''         While Await e.MoveNextAsync()
'''             Console.Write(e.Current)
'''         End While
'''     Finally
'''         Await e.DisposeAsync()
'''     End Try
''' End Using
''' </code>
''' </example>
Public Class KindroidClient
    Implements IDisposable

    ' ── Constants ────────────────────────────────────────────────────────────

    ''' <summary>Maximum page size accepted by <c>get-chat-messages</c>.</summary>
    Public Const MaxPageSize As Integer = 100

    Private Shared ReadOnly JsonOpts As New JsonSerializerOptions() With {
        .PropertyNameCaseInsensitive = True,
        .DefaultIgnoreCondition = Serialization.JsonIgnoreCondition.WhenWritingNull
    }

    ' ── Fields ───────────────────────────────────────────────────────────────

    Private ReadOnly _http As HttpClient
    Private ReadOnly _ownsHttp As Boolean
    Private ReadOnly _apiKey As String
    Private ReadOnly _baseUrl As String
    Private ReadOnly _maxRetries As Integer
    Private ReadOnly _retryDelay As TimeSpan
    Private _disposed As Boolean

    ' ── Constructors ─────────────────────────────────────────────────────────

    ''' <summary>
    ''' Initialises a new <see cref="KindroidClient"/> with the given API key and default options.
    ''' An internal <see cref="HttpClient"/> is created and disposed with this instance.
    ''' </summary>
    ''' <param name="apiKey">Your <c>kn_…</c> API key.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="apiKey"/> is <see langword="Nothing"/> or empty.</exception>
    Public Sub New(apiKey As String)
        Me.New(apiKey, New KindroidClientOptions())
    End Sub

    ''' <summary>
    ''' Initialises a new <see cref="KindroidClient"/> with the given API key and options.
    ''' An internal <see cref="HttpClient"/> is created and disposed with this instance.
    ''' </summary>
    ''' <param name="apiKey">Your <c>kn_…</c> API key.</param>
    ''' <param name="options">Client options.</param>
    ''' <exception cref="ArgumentNullException"><paramref name="apiKey"/> is <see langword="Nothing"/> or empty, or <paramref name="options"/> is <see langword="Nothing"/>.</exception>
    Public Sub New(apiKey As String, options As KindroidClientOptions)
        ArgumentNullException.ThrowIfNull(options)
        If String.IsNullOrWhiteSpace(apiKey) Then
            Throw New ArgumentNullException(NameOf(apiKey), "A Kindroid API Key is required.")
        End If

        _http = New HttpClient() With {.Timeout = options.Timeout}
        _ownsHttp = True
        _apiKey = apiKey
        _baseUrl = NormalizeBaseUrl(options)
        _maxRetries = options.MaxRetries
        _retryDelay = options.RetryDelay
    End Sub

    ''' <summary>
    ''' Initialises a new <see cref="KindroidClient"/> using a pre-configured <see cref="HttpClient"/>
    ''' (for example from <c>IHttpClientFactory</c>). The caller owns and disposes it, and must configure the
    ''' <c>Authorization: Bearer kn_…</c> header on it. Prefer <see cref="New(HttpClient, String, KindroidClientOptions)"/>
    ''' to let the library attach the key for you.
    ''' </summary>
    ''' <param name="httpClient">A configured <see cref="HttpClient"/> that already carries the Authorization header.</param>
    Public Sub New(httpClient As HttpClient)
        Me.New(httpClient, Nothing, New KindroidClientOptions())
    End Sub

    ''' <summary>
    ''' Initialises a new <see cref="KindroidClient"/> on top of a caller-owned <see cref="HttpClient"/>,
    ''' sending <paramref name="apiKey"/> as the bearer token on every request.
    ''' <see cref="KindroidClientOptions.Timeout"/> is ignored because the caller controls the client's timeout.
    ''' </summary>
    ''' <param name="httpClient">The <see cref="HttpClient"/> to use. It is not disposed by this class.</param>
    ''' <param name="apiKey">Your <c>kn_…</c> API key, or <see langword="Nothing"/> if the client already sets the header.</param>
    ''' <param name="options">Client options.</param>
    Public Sub New(httpClient As HttpClient, apiKey As String, options As KindroidClientOptions)
        ArgumentNullException.ThrowIfNull(httpClient)
        ArgumentNullException.ThrowIfNull(options)
        _http = httpClient
        _ownsHttp = False
        _apiKey = If(String.IsNullOrWhiteSpace(apiKey), Nothing, apiKey)
        _baseUrl = NormalizeBaseUrl(options)
        _maxRetries = options.MaxRetries
        _retryDelay = options.RetryDelay
    End Sub

    Private Shared Function NormalizeBaseUrl(options As KindroidClientOptions) As String
        Return options.BaseUrl.AbsoluteUri.TrimEnd("/"c)
    End Function

    ' ════════════════════════════════════════════════════════════════════════
    '  PRIVATE HELPERS
    ' ════════════════════════════════════════════════════════════════════════

#Region "HTTP Helpers"

    Private Shared Function RequireText(value As String, paramName As String) As String
        If value Is Nothing Then Throw New ArgumentNullException(paramName)
        If String.IsNullOrWhiteSpace(value) Then Throw New ArgumentException("Value cannot be empty or whitespace.", paramName)
        Return value
    End Function

    Private Shared Sub ValidateLimit(limit As Integer?)
        If limit.HasValue AndAlso (limit.Value < 1 OrElse limit.Value > MaxPageSize) Then
            Throw New ArgumentOutOfRangeException(NameOf(limit), limit.Value, $"limit must be between 1 and {MaxPageSize}.")
        End If
    End Sub

    Private Sub ThrowIfDisposed()
        ObjectDisposedException.ThrowIf(_disposed, Me)
    End Sub

    ''' <summary>
    ''' Sends a request, retrying on 429 as configured. Returns only successful responses; every failure becomes a
    ''' <see cref="KindroidException"/> and the failed response is disposed. The caller disposes the returned response.
    ''' </summary>
    Private Async Function SendCoreAsync(method As HttpMethod, pathAndQuery As String, jsonBody As String,
                                         streaming As Boolean, requester As String,
                                         cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
        ThrowIfDisposed()
        Dim attempt = 0
        Do
            Dim failure As KindroidException
            Using request As New HttpRequestMessage(method, New Uri(_baseUrl & pathAndQuery))
                If _apiKey IsNot Nothing Then request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", _apiKey)
                If requester IsNot Nothing Then request.Headers.TryAddWithoutValidation("X-Kindroid-Requester", requester)
                If jsonBody IsNot Nothing Then request.Content = New StringContent(jsonBody, Encoding.UTF8, "application/json")

                Dim completion = If(streaming, HttpCompletionOption.ResponseHeadersRead, HttpCompletionOption.ResponseContentRead)
                Dim response = Await _http.SendAsync(request, completion, cancellationToken).ConfigureAwait(False)
                If response.IsSuccessStatusCode Then Return response

                Try
                    Dim body = Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
                    failure = KindroidException.FromResponse(CInt(response.StatusCode), body, GetRetryAfter(response))
                Finally
                    response.Dispose()
                End Try
            End Using

            If failure.StatusCode = 429 AndAlso attempt < _maxRetries Then
                Dim wait = If(failure.RetryAfter, TimeSpan.FromTicks(_retryDelay.Ticks * (1L << attempt)))
                attempt += 1
                Await Task.Delay(wait, cancellationToken).ConfigureAwait(False)
                Continue Do
            End If
            Throw failure
        Loop
    End Function

    Private Shared Function GetRetryAfter(response As HttpResponseMessage) As TimeSpan?
        Dim ra = response.Headers.RetryAfter
        If ra Is Nothing Then Return Nothing
        If ra.Delta.HasValue Then Return ra.Delta
        If ra.Date.HasValue Then
            Dim d = ra.Date.Value - DateTimeOffset.UtcNow
            Return If(d > TimeSpan.Zero, d, TimeSpan.Zero)
        End If
        Return Nothing
    End Function

    Private Async Function PostForTextAsync(path As String, body As Object, cancellationToken As CancellationToken) As Task(Of String)
        Dim json = JsonSerializer.Serialize(body, body.GetType(), JsonOpts)
        Using response = Await SendCoreAsync(HttpMethod.Post, path, json, False, Nothing, cancellationToken).ConfigureAwait(False)
            Return Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
        End Using
    End Function

    ''' <summary>Streams the body of a POST as it arrives. Lazy: nothing is sent until enumeration starts (GetAsyncEnumerator).</summary>
    Private Function StreamPostAsync(path As String, jsonBody As String,
                                     cancellationToken As CancellationToken) As IAsyncEnumerable(Of String)
        Return New ProducerSequence(Of String)(
            Async Function(writer, token)
                Using response = Await SendCoreAsync(HttpMethod.Post, path, jsonBody, True, Nothing, token).ConfigureAwait(False)
                    Using stream = Await response.Content.ReadAsStreamAsync(token).ConfigureAwait(False)
                        Using reader = StreamingHelper.CreateReader(stream)
                            Dim buffer(StreamingHelper.BufferSize - 1) As Char
                            Dim chunk = Await StreamingHelper.ReadChunkAsync(reader, buffer, token).ConfigureAwait(False)
                            While chunk IsNot Nothing
                                Await writer.WriteAsync(chunk, token).ConfigureAwait(False)
                                chunk = Await StreamingHelper.ReadChunkAsync(reader, buffer, token).ConfigureAwait(False)
                            End While
                        End Using
                    End Using
                End Using
            End Function, cancellationToken)
    End Function

#End Region

    ' ════════════════════════════════════════════════════════════════════════
    '  SINGLE AI ENDPOINTS
    ' ════════════════════════════════════════════════════════════════════════

#Region "Send Message"

    ''' <summary>
    ''' Sends a message to an AI and returns its full reply (synchronous). Generation typically takes 4–6 seconds.
    ''' </summary>
    ''' <param name="aiId">The AI to message.</param>
    ''' <param name="message">The user's message. Must not be empty (whitespace-only is accepted by the server).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The AI's reply as a plain string.</returns>
    ''' <exception cref="ArgumentException"><paramref name="aiId"/> is blank or <paramref name="message"/> is empty.</exception>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Function SendMessage(aiId As String, message As String,
                                Optional cancellationToken As CancellationToken = Nothing) As String
        Return SendMessageAsync(aiId, message, cancellationToken).GetAwaiter().GetResult()
    End Function

    ''' <summary>
    ''' Sends a message to an AI and returns its full reply (async).
    ''' </summary>
    ''' <param name="aiId">The AI to message.</param>
    ''' <param name="message">The user's message. Must not be empty (whitespace-only is accepted by the server).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The AI's reply as a plain string.</returns>
    ''' <exception cref="ArgumentException"><paramref name="aiId"/> is blank or <paramref name="message"/> is empty.</exception>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function SendMessageAsync(aiId As String, message As String,
                                           Optional cancellationToken As CancellationToken = Nothing) As Task(Of String)
        RequireText(aiId, NameOf(aiId))
        If String.IsNullOrEmpty(message) Then Throw New ArgumentException("message cannot be empty.", NameOf(message))
        Dim req As New SendMessageRequest() With {.AiId = aiId, .Message = message, .Stream = False}
        Return Await PostForTextAsync("/send-message", req, cancellationToken).ConfigureAwait(False)
    End Function

    ''' <summary>
    ''' Sends a message to an AI and streams the reply as an async sequence of text chunks.
    ''' <para>
    ''' The sequence is lazy: the request is sent when enumeration starts, and HTTP failures are thrown from
    ''' <c>MoveNextAsync</c> as <see cref="KindroidException"/>. Chunks are raw token fragments and may be
    ''' whitespace-only; concatenate them verbatim. The first chunk was observed to be a single space.
    ''' </para>
    ''' </summary>
    ''' <param name="aiId">The AI to message.</param>
    ''' <param name="message">The user's message.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>An async sequence of text chunks.</returns>
    ''' <exception cref="ArgumentException"><paramref name="aiId"/> is blank or <paramref name="message"/> is empty.</exception>
    Public Function SendMessageStreamAsync(aiId As String,
                                           message As String,
                                           Optional cancellationToken As CancellationToken = Nothing) As IAsyncEnumerable(Of String)
        RequireText(aiId, NameOf(aiId))
        If String.IsNullOrEmpty(message) Then Throw New ArgumentException("message cannot be empty.", NameOf(message))
        Dim req As New SendMessageRequest() With {.AiId = aiId, .Message = message, .Stream = True}
        Return StreamPostAsync("/send-message", JsonSerializer.Serialize(req, JsonOpts), cancellationToken)
    End Function

#End Region

#Region "Chat Break"

    ''' <summary>
    ''' Ends the current chat and resets the AI's short-term memory (synchronous).
    ''' The greeting is appended to the history as the first message of the new conversation;
    ''' earlier messages stay visible through <see cref="GetChatMessages"/>.
    ''' </summary>
    ''' <param name="aiId">The AI to reset.</param>
    ''' <param name="greeting">First message of the new conversation. Must not be empty.</param>
    ''' <param name="wipeCascaded">Also wipe cascaded long-term memory. <b>Permanent.</b></param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="ArgumentException"><paramref name="aiId"/> is blank or <paramref name="greeting"/> is empty.</exception>
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
    ''' <param name="greeting">First message of the new conversation. Must not be empty.</param>
    ''' <param name="wipeCascaded">Also wipe cascaded long-term memory. <b>Permanent.</b></param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="ArgumentException"><paramref name="aiId"/> is blank or <paramref name="greeting"/> is empty.</exception>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function ChatBreakAsync(aiId As String,
                                         greeting As String,
                                         Optional wipeCascaded As Boolean = False,
                                         Optional cancellationToken As CancellationToken = Nothing) As Task
        RequireText(aiId, NameOf(aiId))
        If String.IsNullOrEmpty(greeting) Then Throw New ArgumentException("greeting cannot be empty.", NameOf(greeting))
        Dim req As New ChatBreakRequest() With {.AiId = aiId, .Greeting = greeting, .WipeCascaded = wipeCascaded}
        Await PostForTextAsync("/chat-break", req, cancellationToken).ConfigureAwait(False)
    End Function

#End Region

#Region "Get Chat Messages"

    ''' <summary>
    ''' Retrieves one page of chat history for a Kindroid, oldest first (synchronous).
    ''' <para>
    ''' Without <paramref name="startAfterTimestamp"/> you always get the <b>oldest</b> page. To read the newest
    ''' messages you must page forward (see <see cref="EnumerateChatMessagesAsync"/>). The endpoint is limited to
    ''' 600 requests per 24 hours. An unknown <paramref name="aiId"/> yields an empty page rather than an error.
    ''' </para>
    ''' </summary>
    ''' <param name="aiId">The AI whose history to fetch.</param>
    ''' <param name="limit">Page size, 1–100. Defaults to 50 on the server.</param>
    ''' <param name="startAfterTimestamp">Exclusive cursor: the previous page's <c>LastTimestamp</c> (Unix ms).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The response containing messages and pagination info.</returns>
    ''' <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1–100.</exception>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Function GetChatMessages(aiId As String,
                                    Optional limit As Integer? = Nothing,
                                    Optional startAfterTimestamp As Long? = Nothing,
                                    Optional cancellationToken As CancellationToken = Nothing) As GetChatMessagesResponse
        Return GetChatMessagesAsync(aiId, limit, startAfterTimestamp, cancellationToken).GetAwaiter().GetResult()
    End Function

    ''' <summary>
    ''' Retrieves one page of chat history for a Kindroid, oldest first (async). See <see cref="GetChatMessages"/>.
    ''' </summary>
    ''' <param name="aiId">The AI whose history to fetch.</param>
    ''' <param name="limit">Page size, 1–100. Defaults to 50 on the server.</param>
    ''' <param name="startAfterTimestamp">Exclusive cursor: the previous page's <c>LastTimestamp</c> (Unix ms).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The response containing messages and pagination info.</returns>
    ''' <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1–100.</exception>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function GetChatMessagesAsync(aiId As String,
                                               Optional limit As Integer? = Nothing,
                                               Optional startAfterTimestamp As Long? = Nothing,
                                               Optional cancellationToken As CancellationToken = Nothing) As Task(Of GetChatMessagesResponse)
        RequireText(aiId, NameOf(aiId))
        ValidateLimit(limit)
        Return Await GetChatMessagesInternalAsync(aiId, Nothing, limit, startAfterTimestamp, cancellationToken).ConfigureAwait(False)
    End Function

    ''' <summary>
    ''' Retrieves one page of chat history for a group chat, oldest first (synchronous).
    ''' </summary>
    ''' <param name="groupId">The group chat whose history to fetch.</param>
    ''' <param name="limit">Page size, 1–100. Defaults to 50 on the server.</param>
    ''' <param name="startAfterTimestamp">Exclusive cursor: the previous page's <c>LastTimestamp</c> (Unix ms).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The response containing messages and pagination info.</returns>
    ''' <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1–100.</exception>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Function GetGroupChatMessages(groupId As String,
                                         Optional limit As Integer? = Nothing,
                                         Optional startAfterTimestamp As Long? = Nothing,
                                         Optional cancellationToken As CancellationToken = Nothing) As GetChatMessagesResponse
        Return GetGroupChatMessagesAsync(groupId, limit, startAfterTimestamp, cancellationToken).GetAwaiter().GetResult()
    End Function

    ''' <summary>
    ''' Retrieves one page of chat history for a group chat, oldest first (async).
    ''' </summary>
    ''' <param name="groupId">The group chat whose history to fetch.</param>
    ''' <param name="limit">Page size, 1–100. Defaults to 50 on the server.</param>
    ''' <param name="startAfterTimestamp">Exclusive cursor: the previous page's <c>LastTimestamp</c> (Unix ms).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The response containing messages and pagination info.</returns>
    ''' <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1–100.</exception>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function GetGroupChatMessagesAsync(groupId As String,
                                                    Optional limit As Integer? = Nothing,
                                                    Optional startAfterTimestamp As Long? = Nothing,
                                                    Optional cancellationToken As CancellationToken = Nothing) As Task(Of GetChatMessagesResponse)
        RequireText(groupId, NameOf(groupId))
        ValidateLimit(limit)
        Return Await GetChatMessagesInternalAsync(Nothing, groupId, limit, startAfterTimestamp, cancellationToken).ConfigureAwait(False)
    End Function

    ''' <summary>
    ''' Enumerates the history of a Kindroid page by page, oldest first, until the server reports no more pages.
    ''' Each page costs one request against the 600-per-day budget, so use a large <paramref name="pageSize"/>.
    ''' </summary>
    ''' <param name="aiId">The AI whose history to read.</param>
    ''' <param name="pageSize">Messages per request, 1–100 (default 100).</param>
    ''' <param name="startAfterTimestamp">Optional exclusive start cursor (Unix ms).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>A lazy async sequence of messages.</returns>
    Public Function EnumerateChatMessagesAsync(aiId As String,
                                               Optional pageSize As Integer = MaxPageSize,
                                               Optional startAfterTimestamp As Long? = Nothing,
                                               Optional cancellationToken As CancellationToken = Nothing) As IAsyncEnumerable(Of ChatMessage)
        RequireText(aiId, NameOf(aiId))
        ValidateLimit(pageSize)
        Return PageIterator(aiId, Nothing, pageSize, startAfterTimestamp, cancellationToken)
    End Function

    ''' <summary>
    ''' Enumerates the history of a group chat page by page, oldest first. See <see cref="EnumerateChatMessagesAsync"/>.
    ''' </summary>
    ''' <param name="groupId">The group chat whose history to read.</param>
    ''' <param name="pageSize">Messages per request, 1–100 (default 100).</param>
    ''' <param name="startAfterTimestamp">Optional exclusive start cursor (Unix ms).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>A lazy async sequence of messages.</returns>
    Public Function EnumerateGroupChatMessagesAsync(groupId As String,
                                                    Optional pageSize As Integer = MaxPageSize,
                                                    Optional startAfterTimestamp As Long? = Nothing,
                                                    Optional cancellationToken As CancellationToken = Nothing) As IAsyncEnumerable(Of ChatMessage)
        RequireText(groupId, NameOf(groupId))
        ValidateLimit(pageSize)
        Return PageIterator(Nothing, groupId, pageSize, startAfterTimestamp, cancellationToken)
    End Function

    Private Function PageIterator(aiId As String, groupId As String, pageSize As Integer,
                                  startAfterTimestamp As Long?,
                                  cancellationToken As CancellationToken) As IAsyncEnumerable(Of ChatMessage)
        Return New ProducerSequence(Of ChatMessage)(
            Async Function(writer, token)
                Dim cursor = startAfterTimestamp
                Do
                    Dim page = Await GetChatMessagesInternalAsync(aiId, groupId, pageSize, cursor, token).ConfigureAwait(False)
                    For Each m In page.Messages
                        Await writer.WriteAsync(m, token).ConfigureAwait(False)
                    Next
                    If Not page.Pagination.HasMore OrElse Not page.Pagination.LastTimestamp.HasValue Then Exit Do
                    cursor = page.Pagination.LastTimestamp
                Loop
            End Function, cancellationToken)
    End Function

    Private Async Function GetChatMessagesInternalAsync(aiId As String, groupId As String, limit As Integer?,
                                                        startAfterTimestamp As Long?, cancellationToken As CancellationToken) As Task(Of GetChatMessagesResponse)
        Dim qs As New StringBuilder("/get-chat-messages?")
        If aiId IsNot Nothing Then
            qs.Append("ai_id=").Append(Uri.EscapeDataString(aiId))
        Else
            qs.Append("group_id=").Append(Uri.EscapeDataString(groupId))
        End If
        If limit.HasValue Then qs.Append("&limit=").Append(limit.Value)
        If startAfterTimestamp.HasValue Then qs.Append("&start_after_timestamp=").Append(startAfterTimestamp.Value)

        Using response = Await SendCoreAsync(HttpMethod.Get, qs.ToString(), Nothing, False, Nothing, cancellationToken).ConfigureAwait(False)
            ' The server labels JSON as text/plain, so always read as a string and parse ourselves.
            Dim json = Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
            Try
                Dim result = JsonSerializer.Deserialize(Of GetChatMessagesResponse)(json, JsonOpts)
                If result Is Nothing Then Throw New KindroidException("The server returned an empty chat history response.")
                Return result
            Catch ex As JsonException
                Throw New KindroidException("The server returned a chat history response that could not be parsed.", ex)
            End Try
        End Using
    End Function

#End Region

#Region "Rewind Messages"

    ''' <summary>
    ''' Removes the most-recent messages from a single-AI conversation (synchronous).
    ''' <para>
    ''' <paramref name="count"/> must be <b>even</b>: the chat has to end on an AI message after the rewind, so whole
    ''' user/AI exchanges are removed. This is checked locally; the server answers odd counts with 400.
    ''' An unknown <paramref name="aiId"/> yields 404.
    ''' </para>
    ''' </summary>
    ''' <param name="aiId">The AI to rewind.</param>
    ''' <param name="count">Number of most-recent messages to remove (even, ≥ 2).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 2 or odd.</exception>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Sub RewindMessages(aiId As String,
                              count As Integer,
                              Optional cancellationToken As CancellationToken = Nothing)
        RewindMessagesAsync(aiId, count, cancellationToken).GetAwaiter().GetResult()
    End Sub

    ''' <summary>
    ''' Removes the most-recent messages from a single-AI conversation (async). See <see cref="RewindMessages"/>.
    ''' </summary>
    ''' <param name="aiId">The AI to rewind.</param>
    ''' <param name="count">Number of most-recent messages to remove (even, ≥ 2).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 2 or odd.</exception>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function RewindMessagesAsync(aiId As String,
                                              count As Integer,
                                              Optional cancellationToken As CancellationToken = Nothing) As Task
        RequireText(aiId, NameOf(aiId))
        If count < 2 OrElse count Mod 2 <> 0 Then
            Throw New ArgumentOutOfRangeException(NameOf(count), count, "For a single AI, count must be an even number ≥ 2 (whole user/AI exchanges).")
        End If
        Dim req As New RewindMessagesRequest() With {.AiId = aiId, .Count = count}
        Await PostForTextAsync("/rewind-messages", req, cancellationToken).ConfigureAwait(False)
    End Function

    ''' <summary>
    ''' Removes the most-recent messages from a group chat (synchronous). Any count ≥ 1 is allowed.
    ''' </summary>
    ''' <param name="groupId">The group chat to rewind.</param>
    ''' <param name="count">Number of most-recent messages to remove (≥ 1).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 1.</exception>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Sub RewindGroupMessages(groupId As String,
                                   count As Integer,
                                   Optional cancellationToken As CancellationToken = Nothing)
        RewindGroupMessagesAsync(groupId, count, cancellationToken).GetAwaiter().GetResult()
    End Sub

    ''' <summary>
    ''' Removes the most-recent messages from a group chat (async). Any count ≥ 1 is allowed.
    ''' </summary>
    ''' <param name="groupId">The group chat to rewind.</param>
    ''' <param name="count">Number of most-recent messages to remove (≥ 1).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 1.</exception>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function RewindGroupMessagesAsync(groupId As String,
                                                   count As Integer,
                                                   Optional cancellationToken As CancellationToken = Nothing) As Task
        RequireText(groupId, NameOf(groupId))
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1)
        Dim req As New RewindMessagesRequest() With {.GroupId = groupId, .Count = count}
        Await PostForTextAsync("/rewind-messages", req, cancellationToken).ConfigureAwait(False)
    End Function

#End Region

#Region "Update AI Info"

    ''' <summary>
    ''' Updates the persona and configuration of an existing AI (synchronous).
    ''' Only populate the fields you want to change; <see langword="Nothing"/> fields are not sent. An empty string
    ''' is sent and clears the field. Unknown AI → <see cref="KindroidNotFoundException"/>.
    ''' </summary>
    ''' <param name="request">The update request. <see cref="UpdateAiInfoRequest.AiId"/> is required.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Sub UpdateAiInfo(request As UpdateAiInfoRequest,
                            Optional cancellationToken As CancellationToken = Nothing)
        UpdateAiInfoAsync(request, cancellationToken).GetAwaiter().GetResult()
    End Sub

    ''' <summary>
    ''' Updates the persona and configuration of an existing AI (async). See <see cref="UpdateAiInfo"/>.
    ''' </summary>
    ''' <param name="request">The update request. <see cref="UpdateAiInfoRequest.AiId"/> is required.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function UpdateAiInfoAsync(request As UpdateAiInfoRequest,
                                            Optional cancellationToken As CancellationToken = Nothing) As Task
        ArgumentNullException.ThrowIfNull(request)
        RequireText(request.AiId, NameOf(request))
        Await PostForTextAsync("/update-info", request, cancellationToken).ConfigureAwait(False)
    End Function

#End Region

    ' ════════════════════════════════════════════════════════════════════════
    '  GROUP CHAT ENDPOINTS  (require an active Kindroid subscription → otherwise 403)
    ' ════════════════════════════════════════════════════════════════════════

#Region "Group User Message"

    ''' <summary>
    ''' Adds a text message from the user to a group chat (synchronous).
    ''' </summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="message">The user's text message. Must not be empty.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="ArgumentException"><paramref name="groupId"/> is blank or <paramref name="message"/> is empty.</exception>
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
    ''' <param name="message">The user's text message. Must not be empty.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="ArgumentException"><paramref name="groupId"/> is blank or <paramref name="message"/> is empty.</exception>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function GroupUserMessageAsync(groupId As String,
                                                message As String,
                                                Optional cancellationToken As CancellationToken = Nothing) As Task
        RequireText(groupId, NameOf(groupId))
        If String.IsNullOrEmpty(message) Then Throw New ArgumentException("message cannot be empty.", NameOf(message))
        Dim req As New GroupUserMessageRequest() With {.GroupId = groupId, .Message = message}
        Await PostForTextAsync("/groupchats-user-message", req, cancellationToken).ConfigureAwait(False)
    End Function

    ''' <summary>
    ''' Adds a voice message (by URL) from the user to a group chat (synchronous).
    ''' An unreachable or non-audio URL makes the server fail with 500 ("Server error while transcribing").
    ''' </summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="audioUrl">URL of the voice message.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Sub GroupUserAudioMessage(groupId As String,
                                     audioUrl As Uri,
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
                                                     audioUrl As Uri,
                                                     Optional cancellationToken As CancellationToken = Nothing) As Task
        RequireText(groupId, NameOf(groupId))
        ArgumentNullException.ThrowIfNull(audioUrl)
        Dim req As New GroupUserMessageRequest() With {.GroupId = groupId, .AudioUrl = audioUrl.AbsoluteUri}
        Await PostForTextAsync("/groupchats-user-message", req, cancellationToken).ConfigureAwait(False)
    End Function

#End Region

#Region "Group Get Turn"

    ''' <summary>
    ''' Determines which participant should speak next in the group (synchronous).
    ''' </summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="allowUser">Whether the user is allowed to be the next speaker. When <see langword="False"/> the server
    ''' returns an AI even if it would otherwise be the user's turn.</param>
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

    ''' <summary>Determines which participant should speak next in the group (async). See <see cref="GroupGetTurn"/>.</summary>
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
        RequireText(groupId, NameOf(groupId))
        Dim req As New GroupGetTurnRequest() With {.GroupId = groupId, .AllowUser = allowUser}
        Dim body = (Await PostForTextAsync("/groupchats-get-turn", req, cancellationToken).ConfigureAwait(False)).Trim()
        Return New GroupGetTurnResult(If(body.Length = 0, Nothing, body))
    End Function

#End Region

#Region "Group AI Response"

    ''' <summary>
    ''' Generates a response from a specific AI in the group and returns the full reply (synchronous).
    ''' If the AI exists but is not a member of the group, the server answers 200 with an empty body.
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

    ''' <summary>Generates a response from a specific AI in the group (async). See <see cref="GroupAiResponse"/>.</summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="aiId">The AI that should respond.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The AI's reply as a plain string.</returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function GroupAiResponseAsync(groupId As String,
                                               aiId As String,
                                               Optional cancellationToken As CancellationToken = Nothing) As Task(Of String)
        RequireText(groupId, NameOf(groupId))
        RequireText(aiId, NameOf(aiId))
        Dim req As New GroupAiResponseRequest() With {.GroupId = groupId, .AiId = aiId, .Stream = False}
        Return Await PostForTextAsync("/groupchats-ai-response", req, cancellationToken).ConfigureAwait(False)
    End Function

    ''' <summary>
    ''' Generates a response from a specific AI in the group and streams the reply (lazy; same rules as
    ''' <see cref="SendMessageStreamAsync"/>).
    ''' </summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="aiId">The AI that should respond.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>An async sequence of text chunks.</returns>
    ''' <exception cref="ArgumentException"><paramref name="groupId"/> or <paramref name="aiId"/> is blank.</exception>
    Public Function GroupAiResponseStreamAsync(groupId As String,
                                               aiId As String,
                                               Optional cancellationToken As CancellationToken = Nothing) As IAsyncEnumerable(Of String)
        RequireText(groupId, NameOf(groupId))
        RequireText(aiId, NameOf(aiId))
        Dim req As New GroupAiResponseRequest() With {.GroupId = groupId, .AiId = aiId, .Stream = True}
        Return StreamPostAsync("/groupchats-ai-response", JsonSerializer.Serialize(req, JsonOpts), cancellationToken)
    End Function

    ''' <summary>
    ''' Drives the documented turn loop for you: repeatedly asks <c>get-turn</c> and, while an AI is returned, generates
    ''' its reply, yielding each one. Stops when it becomes the user's turn or after <paramref name="maxTurns"/> replies.
    ''' Call <see cref="GroupUserMessageAsync"/> first. Lazy: nothing is sent until enumeration starts (GetAsyncEnumerator).
    ''' </summary>
    ''' <param name="groupId">The group chat.</param>
    ''' <param name="allowUser">Passed to <c>get-turn</c>. Leave <see langword="True"/> so the cycle can end; with
    ''' <see langword="False"/> the loop only stops at <paramref name="maxTurns"/>.</param>
    ''' <param name="maxTurns">Safety cap on the number of AI replies (default 10).</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>A lazy async sequence of <see cref="GroupAiReply"/>.</returns>
    Public Function GroupRunTurnsAsync(groupId As String,
                                       Optional allowUser As Boolean = True,
                                       Optional maxTurns As Integer = 10,
                                       Optional cancellationToken As CancellationToken = Nothing) As IAsyncEnumerable(Of GroupAiReply)
        RequireText(groupId, NameOf(groupId))
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTurns, 1)
        Return TurnIterator(groupId, allowUser, maxTurns, cancellationToken)
    End Function

    Private Function TurnIterator(groupId As String, allowUser As Boolean, maxTurns As Integer,
                                  cancellationToken As CancellationToken) As IAsyncEnumerable(Of GroupAiReply)
        Return New ProducerSequence(Of GroupAiReply)(
            Async Function(writer, token)
                For turn = 1 To maxTurns
                    Dim nextTurn = Await GroupGetTurnAsync(groupId, allowUser, token).ConfigureAwait(False)
                    If nextTurn.IsUserTurn Then Exit For
                    Dim text = Await GroupAiResponseAsync(groupId, nextTurn.AiId, token).ConfigureAwait(False)
                    Await writer.WriteAsync(New GroupAiReply(nextTurn.AiId, text), token).ConfigureAwait(False)
                Next
            End Function, cancellationToken)
    End Function

#End Region

#Region "Group Chat Break"

    ''' <summary>
    ''' Ends the current group conversation and resets short-term memory (synchronous).
    ''' </summary>
    ''' <param name="groupId">The group chat to reset.</param>
    ''' <param name="greeting">First message of the new conversation. Must not be empty.</param>
    ''' <param name="wipeCascaded">Also wipe cascaded long-term memory. <b>Permanent.</b></param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="ArgumentException"><paramref name="groupId"/> is blank or <paramref name="greeting"/> is empty.</exception>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Sub GroupChatBreak(groupId As String,
                              greeting As String,
                              Optional wipeCascaded As Boolean = False,
                              Optional cancellationToken As CancellationToken = Nothing)
        GroupChatBreakAsync(groupId, greeting, wipeCascaded, cancellationToken).GetAwaiter().GetResult()
    End Sub

    ''' <summary>Ends the current group conversation and resets short-term memory (async).</summary>
    ''' <param name="groupId">The group chat to reset.</param>
    ''' <param name="greeting">First message of the new conversation. Must not be empty.</param>
    ''' <param name="wipeCascaded">Also wipe cascaded long-term memory. <b>Permanent.</b></param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="ArgumentException"><paramref name="groupId"/> is blank or <paramref name="greeting"/> is empty.</exception>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function GroupChatBreakAsync(groupId As String,
                                              greeting As String,
                                              Optional wipeCascaded As Boolean = False,
                                              Optional cancellationToken As CancellationToken = Nothing) As Task
        RequireText(groupId, NameOf(groupId))
        If String.IsNullOrEmpty(greeting) Then Throw New ArgumentException("greeting cannot be empty.", NameOf(greeting))
        Dim req As New GroupChatBreakRequest() With {.GroupId = groupId, .Greeting = greeting, .WipeCascaded = wipeCascaded}
        Await PostForTextAsync("/groupchats-chat-break", req, cancellationToken).ConfigureAwait(False)
    End Function

#End Region

#Region "Update Group Info"

    ''' <summary>
    ''' Updates the configuration of an existing group chat (synchronous).
    ''' Only populate the fields you want to change. If <see cref="UpdateGroupInfoRequest.AiList"/> is set it must
    ''' contain at least one id and every id must be a valid Kindroid.
    ''' </summary>
    ''' <param name="request">The update request. <see cref="UpdateGroupInfoRequest.GroupId"/> is required.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Sub UpdateGroupInfo(request As UpdateGroupInfoRequest,
                               Optional cancellationToken As CancellationToken = Nothing)
        UpdateGroupInfoAsync(request, cancellationToken).GetAwaiter().GetResult()
    End Sub

    ''' <summary>Updates the configuration of an existing group chat (async). See <see cref="UpdateGroupInfo"/>.</summary>
    ''' <param name="request">The update request. <see cref="UpdateGroupInfoRequest.GroupId"/> is required.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function UpdateGroupInfoAsync(request As UpdateGroupInfoRequest,
                                               Optional cancellationToken As CancellationToken = Nothing) As Task
        ArgumentNullException.ThrowIfNull(request)
        RequireText(request.GroupId, NameOf(request))
        If request.AiList IsNot Nothing AndAlso request.AiList.Count = 0 Then
            Throw New ArgumentException("AiList must contain at least one AI id when provided.", NameOf(request))
        End If
        Await PostForTextAsync("/groupchats-update", request, cancellationToken).ConfigureAwait(False)
    End Function

#End Region

    ' ════════════════════════════════════════════════════════════════════════
    '  DISCORD BOT ENDPOINT  (implemented from the docs; not verified live - needs a share code)
    ' ════════════════════════════════════════════════════════════════════════

#Region "Discord Bot"

    ''' <summary>
    ''' Computes the <c>X-Kindroid-Requester</c> value recommended by Kindroid for a Discord user: the base64 of the
    ''' URI-encoded username, stripped to alphanumerics and cut to 32 characters (mirrors the JavaScript sample).
    ''' </summary>
    ''' <param name="username">The Discord username of the person who triggered the call.</param>
    ''' <returns>A 32-character (or shorter) alphanumeric identifier.</returns>
    Public Shared Function HashRequester(username As String) As String
        ArgumentNullException.ThrowIfNull(username)
        ' JavaScript's encodeURIComponent additionally leaves ! * ' ( ) unescaped.
        Dim encoded = Uri.EscapeDataString(username).
            Replace("%21", "!", StringComparison.Ordinal).Replace("%2A", "*", StringComparison.Ordinal).
            Replace("%27", "'", StringComparison.Ordinal).Replace("%28", "(", StringComparison.Ordinal).
            Replace("%29", ")", StringComparison.Ordinal)
        Dim b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(encoded))
        Dim clean = Regex.Replace(b64, "[^a-zA-Z0-9]", String.Empty, RegexOptions.None, TimeSpan.FromSeconds(1))
        Return If(clean.Length > 32, clean.Substring(0, 32), clean)
    End Function

    ''' <summary>
    ''' Sends a Discord conversation to <c>POST /discord-bot</c> and returns the reply (synchronous).
    ''' </summary>
    ''' <param name="request">The share code, filter flag and conversation.</param>
    ''' <param name="requester">Value for the <c>X-Kindroid-Requester</c> header, see <see cref="HashRequester"/>.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The reply text.</returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Function DiscordBot(request As DiscordBotRequest,
                               requester As String,
                               Optional cancellationToken As CancellationToken = Nothing) As String
        Return DiscordBotAsync(request, requester, cancellationToken).GetAwaiter().GetResult()
    End Function

    ''' <summary>
    ''' Sends a Discord conversation to <c>POST /discord-bot</c> and returns the reply (async).
    ''' </summary>
    ''' <param name="request">The share code, filter flag and conversation.</param>
    ''' <param name="requester">Value for the <c>X-Kindroid-Requester</c> header, see <see cref="HashRequester"/>.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The reply text.</returns>
    ''' <exception cref="KindroidException">The API returned a non-success status.</exception>
    Public Async Function DiscordBotAsync(request As DiscordBotRequest,
                                          requester As String,
                                          Optional cancellationToken As CancellationToken = Nothing) As Task(Of String)
        ArgumentNullException.ThrowIfNull(request)
        RequireText(request.ShareCode, NameOf(request))
        RequireText(requester, NameOf(requester))
        If request.Conversation.Count = 0 Then
            Throw New ArgumentException("Conversation must contain at least one message.", NameOf(request))
        End If
        Dim json = JsonSerializer.Serialize(request, JsonOpts)
        Using response = Await SendCoreAsync(HttpMethod.Post, "/discord-bot", json, False, requester, cancellationToken).ConfigureAwait(False)
            Return Await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(False)
        End Using
    End Function

#End Region

    ' ════════════════════════════════════════════════════════════════════════
    '  IDisposable
    ' ════════════════════════════════════════════════════════════════════════

    ''' <inheritdoc/>
    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    ''' <summary>
    ''' Releases the internal <see cref="HttpClient"/> when this instance created it.
    ''' </summary>
    ''' <param name="disposing"><see langword="True"/> when called from <see cref="Dispose()"/>.</param>
    Protected Overridable Sub Dispose(disposing As Boolean)
        If _disposed Then Return
        If disposing Then
            If _ownsHttp Then _http.Dispose()
        End If
        _disposed = True
    End Sub

End Class
