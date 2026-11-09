Imports System.Collections.ObjectModel
Imports System.Text.Json.Serialization

Namespace Models

    ''' <summary>
    ''' One line of the conversation sent to <c>POST /discord-bot</c>.
    ''' </summary>
    Public NotInheritable Class DiscordConversationMessage

        ''' <summary>
        ''' The Discord username that wrote the line.
        ''' </summary>
        ''' <returns>A <see cref="String"/> containing the username.</returns>
        <JsonPropertyName("username")>
        Public Property Username As String = String.Empty

        ''' <summary>
        ''' The text of the line.
        ''' </summary>
        ''' <returns>A <see cref="String"/> containing the message text.</returns>
        <JsonPropertyName("text")>
        Public Property Text As String = String.Empty

        ''' <summary>
        ''' ISO-8601 timestamp (the equivalent of JavaScript's <c>msg.createdAt.toISOString()</c>).
        ''' </summary>
        ''' <returns>A <see cref="String"/> containing the timestamp.</returns>
        <JsonPropertyName("timestamp")>
        Public Property Timestamp As String = String.Empty

    End Class

    ''' <summary>
    ''' Request body for <c>POST /discord-bot</c>.
    ''' </summary>
    Public NotInheritable Class DiscordBotRequest

        ''' <summary>
        ''' The 5-letter share code of the Kindroid (must be shared by the authenticated account).
        ''' </summary>
        ''' <returns>A <see cref="String"/> containing the share code.</returns>
        <JsonPropertyName("share_code")>
        Public Property ShareCode As String = String.Empty

        ''' <summary>
        ''' Whether the content filter is enabled. Recommended for public servers.
        ''' </summary>
        ''' <returns>A <see cref="Boolean"/> indicating whether the filter is enabled.</returns>
        <JsonPropertyName("enable_filter")>
        Public Property EnableFilter As Boolean = True

        ''' <summary>
        ''' The conversation so far, oldest first.
        ''' </summary>
        ''' <returns>A collection of <see cref="DiscordConversationMessage"/>.</returns>
        <JsonPropertyName("conversation")>
        Public ReadOnly Property Conversation As Collection(Of DiscordConversationMessage) = New Collection(Of DiscordConversationMessage)()

    End Class

    ''' <summary>
    ''' One AI reply produced while driving a group turn loop (see <c>KindroidClient.GroupRunTurnsAsync</c>).
    ''' </summary>
    Public NotInheritable Class GroupAiReply

        ''' <summary>
        ''' The AI that spoke.
        ''' </summary>
        ''' <returns>A <see cref="String"/> containing the AI id.</returns>
        Public ReadOnly Property AiId As String

        ''' <summary>
        ''' The full reply. May be empty: the server returns an empty body for an AI that is not a roster member.
        ''' </summary>
        ''' <returns>A <see cref="String"/> containing the reply text.</returns>
        Public ReadOnly Property Text As String

        ''' <summary>
        ''' Initializes a new instance of the <see cref="GroupAiReply"/> class.
        ''' </summary>
        ''' <param name="aiId">The AI that spoke.</param>
        ''' <param name="text">The full reply.</param>
        Public Sub New(aiId As String, text As String)
            Me.AiId = aiId
            Me.Text = text
        End Sub

    End Class

End Namespace
