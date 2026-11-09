Namespace Exceptions

    ''' <summary>
    ''' Represents an error returned by the Kindroid API.
    ''' <para>
    ''' The server answers failures with a short plain-text body (for example <c>AI not found</c> or
    ''' <c>"limit" must be less than or equal to 100</c>). That text is available in <see cref="ResponseBody"/>.
    ''' Some statuses are surfaced as dedicated subclasses so they can be caught separately:
    ''' <see cref="KindroidAuthenticationException"/> (401), <see cref="KindroidForbiddenException"/> (403),
    ''' <see cref="KindroidNotFoundException"/> (404) and <see cref="KindroidRateLimitException"/> (429).
    ''' </para>
    ''' </summary>
    Public Class KindroidException
        Inherits Exception

        ''' <summary>
        ''' The HTTP status code returned by the API, or <c>0</c> when the failure did not come from an HTTP response.
        ''' </summary>
        ''' <returns>An integer representing the HTTP status code.</returns>
        Public ReadOnly Property StatusCode As Integer

        ''' <summary>
        ''' The raw response body returned by the API (usually a short plain-text description).
        ''' </summary>
        ''' <returns>A string containing the response body.</returns>
        Public ReadOnly Property ResponseBody As String = String.Empty

        ''' <summary>
        ''' How long the server asked the caller to wait before retrying (from the <c>Retry-After</c> header), if it sent one.
        ''' The Kindroid server has not been observed to send this header, so it is normally <see langword="Nothing"/>.
        ''' </summary>
        ''' <returns>A <see cref="TimeSpan"/>, or <see langword="Nothing"/> when no hint was provided.</returns>
        Public ReadOnly Property RetryAfter As TimeSpan?

        ''' <summary>
        ''' Initializes a new instance of the <see cref="KindroidException"/> class.
        ''' </summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="KindroidException"/> class with a specified error message.
        ''' </summary>
        ''' <param name="message">The message that describes the error.</param>
        Public Sub New(message As String)
            MyBase.New(message)
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="KindroidException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        ''' </summary>
        ''' <param name="message">The message that describes the error.</param>
        ''' <param name="innerException">The exception that is the cause of the current exception.</param>
        Public Sub New(message As String, innerException As Exception)
            MyBase.New(message, innerException)
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="KindroidException"/> class with the specified status code and response body.
        ''' </summary>
        ''' <param name="statusCode">The HTTP status code returned by the API.</param>
        ''' <param name="responseBody">The response body returned by the API.</param>
        Public Sub New(statusCode As Integer, responseBody As String)
            Me.New(statusCode, responseBody, CType(Nothing, TimeSpan?))
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="KindroidException"/> class with the specified status code, response body, and retry hint.
        ''' </summary>
        ''' <param name="statusCode">The HTTP status code returned by the API.</param>
        ''' <param name="responseBody">The response body returned by the API.</param>
        ''' <param name="retryAfter">Optional server-provided wait time before retrying.</param>
        Public Sub New(statusCode As Integer, responseBody As String, retryAfter As TimeSpan?)
            MyBase.New(BuildMessage(statusCode, responseBody))
            Me.StatusCode = statusCode
            Me.ResponseBody = If(responseBody, String.Empty)
            Me.RetryAfter = retryAfter
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="KindroidException"/> class with the specified status code, response body, and inner exception.
        ''' </summary>
        ''' <param name="statusCode">The HTTP status code returned by the API.</param>
        ''' <param name="responseBody">The response body returned by the API.</param>
        ''' <param name="innerException">The exception that is the cause of the current exception.</param>
        Public Sub New(statusCode As Integer, responseBody As String, innerException As Exception)
            MyBase.New(BuildMessage(statusCode, responseBody), innerException)
            Me.StatusCode = statusCode
            Me.ResponseBody = If(responseBody, String.Empty)
        End Sub

        ''' <summary>
        ''' Creates the most specific exception type for an HTTP failure.
        ''' </summary>
        ''' <param name="statusCode">The HTTP status code returned by the API.</param>
        ''' <param name="responseBody">The response body returned by the API.</param>
        ''' <param name="retryAfter">Optional server-provided wait time before retrying.</param>
        ''' <returns>A <see cref="KindroidException"/> (or subclass) describing the failure.</returns>
        Public Shared Function FromResponse(statusCode As Integer, responseBody As String, Optional retryAfter As TimeSpan? = Nothing) As KindroidException
            Select Case statusCode
                Case 401
                    Return New KindroidAuthenticationException(responseBody)
                Case 403
                    Return New KindroidForbiddenException(responseBody)
                Case 404
                    Return New KindroidNotFoundException(responseBody)
                Case 429
                    Return New KindroidRateLimitException(responseBody, retryAfter)
                Case Else
                    Return New KindroidException(statusCode, responseBody, retryAfter)
            End Select
        End Function

        Private Shared Function BuildMessage(statusCode As Integer, body As String) As String
            Dim description As String
            Select Case statusCode
                Case 400
                    description = "Bad Request"
                Case 401
                    description = "Unauthorized - check your API key"
                Case 403
                    Description = "Forbidden - group chats and some features need an active subscription"
                Case 404
                    description = "Not Found - the AI or group was not found"
                Case 429
                    description = "Too Many Requests - you have exceeded your rate limit"
                Case 500
                    description = "Internal Server Error"
                Case Else
                    description = "Unexpected status"
            End Select
            Dim detail = If(String.IsNullOrWhiteSpace(body), String.Empty, $": {body.Trim()}")
            Return $"Kindroid API error {statusCode} ({description}){detail}"
        End Function
    End Class

    ''' <summary>The API key was missing or invalid (HTTP 401).</summary>
    <System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification:="Bound to a fixed HTTP status; always created from a response body via KindroidException.FromResponse.")>
    Public NotInheritable Class KindroidAuthenticationException
        Inherits KindroidException

        ''' <summary>Initializes a new instance with the response body.</summary>
        ''' <param name="responseBody">The response body returned by the API.</param>
        Public Sub New(responseBody As String)
            MyBase.New(401, responseBody)
        End Sub
    End Class

    ''' <summary>The account is not allowed to use the endpoint, e.g. group chat without a subscription (HTTP 403).</summary>
    <System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification:="Bound to a fixed HTTP status; always created from a response body via KindroidException.FromResponse.")>
    Public NotInheritable Class KindroidForbiddenException
        Inherits KindroidException

        ''' <summary>Initializes a new instance with the response body.</summary>
        ''' <param name="responseBody">The response body returned by the API.</param>
        Public Sub New(responseBody As String)
            MyBase.New(403, responseBody)
        End Sub
    End Class

    ''' <summary>The AI or group does not exist (HTTP 404). Note the server reports some unknown ids as 400 instead.</summary>
    <System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification:="Bound to a fixed HTTP status; always created from a response body via KindroidException.FromResponse.")>
    Public NotInheritable Class KindroidNotFoundException
        Inherits KindroidException

        ''' <summary>Initializes a new instance with the response body.</summary>
        ''' <param name="responseBody">The response body returned by the API.</param>
        Public Sub New(responseBody As String)
            MyBase.New(404, responseBody)
        End Sub
    End Class

    ''' <summary>
    ''' The per-key rate limit was hit (HTTP 429, body <c>Too Many Requests (API limit)</c>).
    ''' The limiter applies to every request of the key, so wait before sending anything else.
    ''' </summary>
    <System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification:="Bound to a fixed HTTP status; always created from a response body via KindroidException.FromResponse.")>
    Public NotInheritable Class KindroidRateLimitException
        Inherits KindroidException

        ''' <summary>Initializes a new instance with the response body and optional retry hint.</summary>
        ''' <param name="responseBody">The response body returned by the API.</param>
        ''' <param name="retryAfter">Optional server-provided wait time before retrying.</param>
        Public Sub New(responseBody As String, retryAfter As TimeSpan?)
            MyBase.New(429, responseBody, retryAfter)
        End Sub
    End Class

End Namespace
