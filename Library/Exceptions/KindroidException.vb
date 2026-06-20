Namespace Exceptions

    ''' <summary>
    ''' Represents an error returned by the Kindroid API
    ''' </summary>
    Public Class KindroidException
        Inherits Exception

        ''' <summary>
        ''' The HTTP status code returned by the API.
        ''' </summary>
        ''' <returns>An integer representing the HTTP status code.</returns>
        Public ReadOnly Property StatusCode As Integer

        ''' <summary>
        ''' The response body returned by the API.
        ''' </summary>
        ''' <returns>A string containing the response body.</returns>
        Public ReadOnly Property ResponseBody As String

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
            MyBase.New(BuildMessage(statusCode, responseBody))
            Me.StatusCode = statusCode
            Me.ResponseBody = responseBody
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
            Me.ResponseBody = responseBody
        End Sub

        Private Shared Function BuildMessage(statusCode As Integer, body As String) As String
            Dim description = ""
            Select Case statusCode
                Case 400
                    description = "Bad Request"
                Case 401
                    description = "Unauthorized - Check your API Key"
                Case 403
                    description = "Forbidden - Have you subscribed?"
                Case 404
                    description = "Not Found - The AI or group was not found"
                Case 429
                    description = "Too Many Requests - You have exceeded your rate limit"
                Case 500
                    description = "Internal Server Error"
                Case Else
                    description = "Unknown Error"
            End Select
            Dim detail = If(String.IsNullOrWhiteSpace(body), String.Empty, $" :{body.Trim()}")
            Return $"Kindroid API Error {statusCode} ({description}{detail})"
        End Function
    End Class

End Namespace
