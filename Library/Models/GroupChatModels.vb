Imports System.Text.Json.Serialization

Namespace Models

    ' ── Group User Message ────────────────────────────────────────────────────────

    ''' <summary>
    ''' Request body for <c>POST /groupchats-user-message</c>.
    ''' </summary>
    Public NotInheritable Class GroupUserMessageRequest

        ''' <summary>
        ''' The group chat ID to send the message to.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the group chat ID.</returns>
        <JsonPropertyName("group_id")>
        Public Property GroupId As String = String.Empty

        ''' <summary>
        ''' The user's text message. Mutually exclusive with <see cref="AudioUrl"/>.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the user's text message.</returns>
        <JsonPropertyName("message")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property Message As String = Nothing

        ''' <summary>
        ''' The URL of a voice mesage. Mutually exclusive with <see cref="Message"/>.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the URL of a voice message.</returns>
        <JsonPropertyName("audio_url")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property AudioUrl As String = Nothing

    End Class

    ' ── Group Get Turn ────────────────────────────────────────────────────────────

    ''' <summary>
    ''' Request body for <c>POST /groupchats-get-turn</c>.
    ''' </summary>
    Public NotInheritable Class GroupGetTurnRequest

        ''' <summary>
        ''' The group chat ID to get the turn for.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the group chat ID.</returns>
        <JsonPropertyName("group_id")>
        Public Property GroupId As String = String.Empty

        ''' <summary>
        ''' Whether the user is allowed to take the next turn.
        ''' </summary>
        ''' <returns>A <see cref="Boolean"/> indicating whether the user is allowed to take the next turn.</returns>
        <JsonPropertyName("allow_user")>
        Public Property AllowUser As Boolean

    End Class

    ''' <summary>
    ''' Result of <c>POST /groupchats-get-turn</c>.
    ''' <see cref="AiId"/> is <see langword="Nothing"/> when it is the user's turn.
    ''' </summary>
    Public NotInheritable Class GroupGetTurnResult

        ''' <summary>
        ''' The AI whose turn it is, or <see langword="Nothing"/> when the API returns an empty body.
        ''' This indicates the user's turn.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the AI whose turn it is, or <see langword="Nothing"/> when it is the user's turn.</returns>
        Public ReadOnly Property AiId As String

        ''' <summary>
        ''' <see langword="True" /> when it is the user's turn and AI generation has ended.
        ''' </summary>
        ''' <returns>A <see cref="Boolean"/> indicating whether it is the user's turn and AI generation has ended.</returns>
        Public ReadOnly Property IsUserTurn As Boolean
            Get
                Return AiId Is Nothing
            End Get
        End Property

        ''' <summary>
        ''' Initializes a new instance of the <see cref="GroupGetTurnResult"/> class with the specified AI ID.
        ''' </summary>
        ''' <param name="aiId">The AI ID whose turn it is, or <see langword="Nothing"/> when it is the user's turn.</param>
        Public Sub New(aiId As String)
            Me.AiId = aiId
        End Sub

    End Class

    ' ── Group AI Response ─────────────────────────────────────────────────────────

    ''' <summary>
    ''' Request body for <c>POST /groupchats-ai-response</c>.
    ''' </summary>
    Public NotInheritable Class GroupAiResponseRequest

        ''' <summary>
        ''' The group chat ID.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the group chat ID.</returns>
        <JsonPropertyName("group_id")>
        Public Property GroupId As String = String.Empty

        ''' <summary>
        ''' The AI that should respond.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the AI ID that should respond.</returns>
        <JsonPropertyName("ai_id")>
        Public Property AiId As String = String.Empty

        ''' <summary>
        ''' Stream the reply as it is generated.
        ''' Set by the client automatically; do not set manually.
        ''' </summary>
        ''' <returns>A <see cref="Boolean"/> indicating whether the reply should be streamed as it is generated.</returns>
        <JsonPropertyName("stream")>
        Public Property Stream As Boolean = False

    End Class

    ' ── Group Chat Break ──────────────────────────────────────────────────────────

    ''' <summary>
    ''' Request body for <c>POST /groupchats-chat-break</c>.
    ''' </summary>
    Public NotInheritable Class GroupChatBreakRequest

        ''' <summary>
        ''' The group chat to reset.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the group chat ID to reset.</returns>
        <JsonPropertyName("group_id")>
        Public Property GroupId As String = String.Empty

        ''' <summary>
        ''' The first message of the new conversation.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the first message of the new conversation.</returns>
        <JsonPropertyName("greeting")>
        Public Property Greeting As String = String.Empty

        ''' <summary>
        ''' Also wipes the group's cascaded long-term memory.
        ''' Defaults to <see langword="False"/> (only short-term memory is reset).
        ''' </summary>
        ''' <returns>A <see cref="Boolean"/> indicating whether the group's cascaded long-term memory should be wiped.</returns>
        <JsonPropertyName("wipe_cascaded")>
        Public Property WipeCascaded As Boolean = False

    End Class

    ' ── Update Group Info ──────────────────────────────────────────────────────────

    ''' <summary>
    ''' Request body for <c>POST /groupchats-update</c>.
    ''' </summary>
    Public NotInheritable Class UpdateGroupInfoRequest

        ''' <summary>
        ''' Required. The group chat to update.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the group chat ID to update.</returns>
        <JsonPropertyName("group_id")>
        Public Property GroupId As String = String.Empty

        ''' <summary>
        ''' The AI IDs in the group roster. At least one when provided.
        ''' </summary>
        ''' <returns>A <see cref="List(Of String)"/> representing the AI IDs in the group roster.</returns>
        <JsonPropertyName("ai_list")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property AiList As List(Of String) = Nothing

        ''' <summary>
        ''' The new name for the group. If not provided, the name will not be updated.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the new name for the group.</returns>
        <JsonPropertyName("group_name")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property GroupName As String = Nothing

        ''' <summary>
        ''' The new context for the group. If not provided, the context will not be updated.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the new context for the group.</returns>
        <JsonPropertyName("group_context")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property GroupContext As String = Nothing

        ''' <summary>
        ''' The new group response directive that applies to all AIs in the group. If not provided, the directive will not be updated.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the new group response directive.</returns>
        <JsonPropertyName("group_directive")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property GroupDirective As String = Nothing

        ''' <summary>
        ''' The most current scene for the group. If not provided, the current scene will not be updated.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the most current scene for the group.</returns>
        <JsonPropertyName("current_scene")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property CurrentScene As String = Nothing

    End Class

End Namespace


