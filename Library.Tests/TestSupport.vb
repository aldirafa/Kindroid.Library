Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports Kindroid.Library

''' <summary>Fake HTTP layer: records requests and returns canned responses, so no network is needed.</summary>
Friend NotInheritable Class FakeHandler
    Inherits HttpMessageHandler

    Private ReadOnly _respond As Func(Of HttpRequestMessage, String, HttpResponseMessage)
    Public ReadOnly Property Requests As New List(Of (Request As HttpRequestMessage, Body As String))()

    Public Sub New(respond As Func(Of HttpRequestMessage, String, HttpResponseMessage))
        _respond = respond
    End Sub

    Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As Threading.CancellationToken) As Task(Of HttpResponseMessage)
        Dim body = If(request.Content Is Nothing, Nothing, Await request.Content.ReadAsStringAsync(cancellationToken))
        Requests.Add((request, body))
        Return _respond(request, body)
    End Function
End Class

''' <summary>A stream that returns one pre-set byte chunk per Read call (simulates token-by-token streaming).</summary>
Friend NotInheritable Class ChunkedStream
    Inherits Stream

    Private ReadOnly _chunks As Queue(Of Byte())

    Public Sub New(chunks As IEnumerable(Of String))
        _chunks = New Queue(Of Byte())(chunks.Select(Function(c) Encoding.UTF8.GetBytes(c)))
    End Sub

    Public Sub New(chunks As IEnumerable(Of Byte()))
        _chunks = New Queue(Of Byte())(chunks)
    End Sub

    Public Overrides Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer
        If _chunks.Count = 0 Then Return 0
        Dim c = _chunks.Dequeue()
        Array.Copy(c, 0, buffer, offset, c.Length)
        Return c.Length
    End Function

    Public Overrides ReadOnly Property CanRead As Boolean = True
    Public Overrides ReadOnly Property CanSeek As Boolean = False
    Public Overrides ReadOnly Property CanWrite As Boolean = False

    Public Overrides ReadOnly Property Length As Long
        Get
            Throw New NotSupportedException()
        End Get
    End Property

    Public Overrides Property Position As Long
        Get
            Throw New NotSupportedException()
        End Get
        Set(value As Long)
            Throw New NotSupportedException()
        End Set
    End Property

    Public Overrides Sub Flush()
    End Sub

    Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
        Throw New NotSupportedException()
    End Function

    Public Overrides Sub SetLength(value As Long)
        Throw New NotSupportedException()
    End Sub

    Public Overrides Sub Write(buffer As Byte(), offset As Integer, count As Integer)
        Throw New NotSupportedException()
    End Sub
End Class

Friend Module TestSupport

    ''' <summary>The real server labels everything text/plain, even JSON.</summary>
    Public Function Text(status As HttpStatusCode, body As String) As HttpResponseMessage
        Return New HttpResponseMessage(status) With {.Content = New StringContent(body, Encoding.UTF8, "text/plain")}
    End Function

    Public Function Make(handler As FakeHandler, Optional options As KindroidClientOptions = Nothing) As KindroidClient
        Dim http As New HttpClient(handler)
        Return New KindroidClient(http, "kn_test", If(options, New KindroidClientOptions()))
    End Function

End Module
