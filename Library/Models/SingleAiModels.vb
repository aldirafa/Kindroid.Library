Imports System.Collections.ObjectModel
Imports System.Text.Json.Serialization

Namespace Models

    ' ── Send Message ────────────────────────────────────────────────────────────

    ''' <summary>
    ''' Request body for <c>POST /send-message</c>.
    ''' </summary>
    Public NotInheritable Class SendMessageRequest

        ''' <summary>
        ''' The AI to message.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the AI ID.</returns>
        <JsonPropertyName("ai_id")>
        Public Property AiId As String = String.Empty

        ''' <summary>
        ''' The user's message.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the user's message.</returns>
        <JsonPropertyName("message")>
        Public Property Message As String = String.Empty

        ''' <summary>
        ''' Stream the reply as it is generated.
        ''' Set by the client automatically; do not set manually.
        ''' </summary>
        ''' <returns>A boolean indicating whether to stream the reply.</returns>
        <JsonPropertyName("stream")>
        Public Property Stream As Boolean = False
    End Class

    ' ── Chat Break ──────────────────────────────────────────────────────────────

    ''' <summary>
    ''' Request body for <c>POST /chat-break</c>.
    ''' </summary>
    Public NotInheritable Class ChatBreakRequest

        ''' <summary>
        ''' The AI to reset.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the AI ID.</returns>
        <JsonPropertyName("ai_id")>
        Public Property AiId As String = String.Empty

        ''' <summary>
        ''' First message of the new conversation.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the first message of the new conversation.</returns>
        <JsonPropertyName("greeting")>
        Public Property Greeting As String = String.Empty

        ''' <summary>
        ''' Also wipe the AI's cascaded long-term memory.
        ''' Defaults to <see langword="False"/> (only short-term memory is reset).
        ''' </summary>
        ''' <returns>A boolean indicating whether to wipe the cascaded long-term memory.</returns>
        <JsonPropertyName("wipe_cascaded")>
        Public Property WipeCascaded As Boolean = False

    End Class

    ' ── Get Chat Messages ────────────────────────────────────────────────────────

    ''' <summary>
    ''' A single message returned by <c>GET /get-chat-messages</c>.
    ''' </summary>
    Public NotInheritable Class ChatMessage

        ''' <summary>
        ''' Message ID.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the message ID.</returns>
        <JsonPropertyName("id")>
        Public Property Id As String = String.Empty

        ''' <summary>
        ''' Sender of the message.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the sender of the message.</returns>
        <JsonPropertyName("sender")>
        Public Property Sender As String = String.Empty

        ''' <summary>
        ''' Type of the sender. The server documents this field but was <b>not observed to send it</b>;
        ''' prefer <see cref="Sender"/> / <see cref="IsFromUser"/> / <see cref="IsFromAi"/>.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the type of the sender, or an empty string when absent.</returns>
        <JsonPropertyName("sender_type")>
        Public Property SenderType As String = String.Empty

        ''' <summary>
        ''' Display name of the sender.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the display name of the sender.</returns>
        <JsonPropertyName("display_name")>
        Public Property DisplayName As String = String.Empty

        ''' <summary>
        ''' A Unix timestamp (in milliseconds) representing when the message was sent.
        ''' </summary>
        ''' <returns>A long representing the Unix timestamp in milliseconds.</returns>
        <JsonPropertyName("timestamp")>
        Public Property Timestamp As Long

        ''' <summary>
        ''' The content of the message.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the content of the message.</returns>
        <JsonPropertyName("message")>
        Public Property Message As String = String.Empty

        ''' <summary>
        ''' A list of images attached to/sent with the message. The server sends <c>null</c> when there are none; it is exposed as an empty collection. <b>The entries are opaque encrypted tokens (they start with <c>!enc:</c>), not browsable URLs</b>; use <see cref="ImageDescription"/> to know what the image shows.
        ''' </summary>
        ''' <returns>A list of strings representing the URLs of images attached to/sent with the message.</returns>
        <JsonPropertyName("image_urls")>
        <System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read-only", Justification:="Needed so JSON deserialization can assign it, and so a server-sent null (observed) becomes an empty collection.")>
        Public Property ImageUrls As Collection(Of String)
            Get
                Return _imageUrls
            End Get
            Set(value As Collection(Of String))
                _imageUrls = If(value, New Collection(Of String)())
            End Set
        End Property
        Private _imageUrls As Collection(Of String) = New Collection(Of String)()

        ''' <summary>
        ''' A description of the image(s) attached to/sent with the message, if any.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the description of the image(s) attached to/sent with the message, if any.</returns>
        <JsonPropertyName("image_description")>
        Public Property ImageDescription As String = String.Empty

        ''' <summary>
        ''' A description of the video(s) attached to/sent with the message, if any.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the description of the video(s) attached to/sent with the message, if any.</returns>
        <JsonPropertyName("video_description")>
        Public Property VideoDescription As String = String.Empty

        ''' <summary>
        ''' If the message was sent with internet browsing capabilities enabled (since in Kindroid, this is per-message basis),
        ''' this property will contain the response from the internet.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the response from the internet, if internet browsing capabilities were enabled for the message.</returns>
        <JsonPropertyName("internet_response")>
        Public Property InternetResponse As String = String.Empty

        ''' <summary>
        ''' In Kindroid, you can also attach a URL to a message, which the AI can then choose to click on and browse.
        ''' This property will contain the URL that was attached to/sent with the message, if any.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the URL that was attached to/sent with the message, if any.</returns>
        <JsonPropertyName("link_url")>
        <System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification:="This is a JSON response field from the Kindroid API. Changing to Uri could break deserialization of non-standard values returned by the server.")>
        Public Property LinkUrl As String = String.Empty

        ''' <summary>
        ''' If the message was sent with a URL attached, this property will contain the title of the webpage at the URL and/or a description of the link, if available.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the title of the webpage at the URL and/or a description of the link, if available.</returns>
        <JsonPropertyName("link_description")>
        Public Property LinkDescription As String = String.Empty

        ''' <summary>
        ''' The name of a document (for example <c>TUGAS 1.docx</c>) attached to the message, if any.
        ''' This field is not in Kindroid's published API reference; it was observed on live responses.
        ''' Only the file name is returned, not its content.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the attached file's name, or an empty string when absent.</returns>
        <JsonPropertyName("file_name")>
        Public Property FileName As String = String.Empty

        ''' <summary>
        ''' <see langword="True"/> when <see cref="Sender"/> is <c>"user"</c>.
        ''' </summary>
        ''' <returns>A <see cref="Boolean"/> indicating whether the user wrote the message.</returns>
        <JsonIgnore>
        Public ReadOnly Property IsFromUser As Boolean
            Get
                Return String.Equals(Sender, "user", StringComparison.OrdinalIgnoreCase)
            End Get
        End Property

        ''' <summary>
        ''' <see langword="True"/> when <see cref="Sender"/> is <c>"ai"</c>.
        ''' </summary>
        ''' <returns>A <see cref="Boolean"/> indicating whether an AI wrote the message.</returns>
        <JsonIgnore>
        Public ReadOnly Property IsFromAi As Boolean
            Get
                Return String.Equals(Sender, "ai", StringComparison.OrdinalIgnoreCase)
            End Get
        End Property

        ''' <summary>
        ''' <see cref="Timestamp"/> converted to a <see cref="DateTimeOffset"/>.
        ''' </summary>
        ''' <returns>The moment the message was sent (UTC).</returns>
        <JsonIgnore>
        Public ReadOnly Property SentAt As DateTimeOffset
            Get
                Return DateTimeOffset.FromUnixTimeMilliseconds(Timestamp)
            End Get
        End Property

    End Class

    ''' <summary>
    ''' Pagination metadata returned alongside a message page.
    ''' </summary>
    Public NotInheritable Class PaginationInfo

        ''' <summary>
        ''' Returns <see langword="True" /> if there are more messages to fetch after the current page; otherwise, <see langword="False" />.
        ''' </summary>
        ''' <returns>A boolean indicating whether there are more messages to fetch after the current page.</returns>
        <JsonPropertyName("hasMore")>
        Public Property HasMore As Boolean

        ''' <summary>
        ''' The timestamp (Unix milliseconds) of the last message in the current page. Pass it as the
        ''' <c>startAfterTimestamp</c> cursor to fetch the next page.
        ''' The server returns <see langword="Nothing"/> (JSON <c>null</c>) when the page is empty.
        ''' </summary>
        ''' <returns>A nullable long representing the Unix timestamp of the last message in the current page.</returns>
        <JsonPropertyName("lastTimestamp")>
        Public Property LastTimestamp As Long?

        ''' <summary>
        ''' How many messages to return per page.
        ''' </summary>
        ''' <returns>An integer representing the number of messages to return per page.</returns>
        <JsonPropertyName("limit")>
        Public Property Limit As Integer

    End Class

    ''' <summary>
    ''' Response envelope for <c>GET /get-chat-messages</c>.
    ''' </summary>
    Public NotInheritable Class GetChatMessagesResponse

        ''' <summary>
        ''' The list of messages in the current page.
        ''' </summary>
        ''' <returns>A list of <see cref="ChatMessage"/> objects representing the messages in the current page.</returns>
        <JsonPropertyName("messages")>
        <System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read-only", Justification:="Needed so JSON deserialization can assign it; a null from the server becomes an empty collection.")>
        Public Property Messages As Collection(Of ChatMessage)
            Get
                Return _messages
            End Get
            Set(value As Collection(Of ChatMessage))
                _messages = If(value, New Collection(Of ChatMessage)())
            End Set
        End Property
        Private _messages As Collection(Of ChatMessage) = New Collection(Of ChatMessage)()

        ''' <summary>
        ''' Pagination metadata for the current page of messages.
        ''' </summary>
        ''' <returns>A <see cref="PaginationInfo"/> object representing the pagination metadata for the current page of messages.</returns>
        <JsonPropertyName("pagination")>
        Public Property Pagination As PaginationInfo = New PaginationInfo()

    End Class

    ' ── Rewind Messages ──────────────────────────────────────────────────────────

    ''' <summary>
    ''' Request body for <c>POST /rewind-messages</c>.
    ''' </summary>
    Public NotInheritable Class RewindMessagesRequest

        ''' <summary>
        ''' The AI to rewind. Mutually exclusive with <see cref="GroupId"/>.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the ID of the AI to rewind.</returns>
        <JsonPropertyName("ai_id")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property AiId As String = Nothing

        ''' <summary>
        ''' The group chat to rewind. Mutually exclusive with <see cref="AiId"/>.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the ID of the group chat to rewind.</returns>
        <JsonPropertyName("group_id")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property GroupId As String = Nothing

        ''' <summary>
        ''' The number of most-recent messages to remove ( &#x2265; 1 ).
        ''' </summary>
        ''' <returns>An integer representing the number of most-recent messages to remove.</returns>
        <JsonPropertyName("count")>
        Public Property Count As Integer

    End Class

    ' ── Update AI Info ────────────────────────────────────────────────────────────

    ''' <summary>
    ''' Request body for <c>POST /update-info</c>.
    ''' All fields except <see cref="AiId"/> are optional.
    ''' </summary>
    Public NotInheritable Class UpdateAiInfoRequest

        ''' <summary>
        ''' Required. The AI to update.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the ID of the AI to update.</returns>
        <JsonPropertyName("ai_id")>
        Public Property AiId As String = String.Empty

        ''' <summary>
        ''' The new name for the AI. If not provided, the AI's name will not be updated.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the new name for the AI.</returns>
        <JsonPropertyName("ai_name")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property AiName As String = Nothing

        ''' <summary>
        ''' The new gender for the AI. If not provided, the AI gender will not be updated.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the new gender for the AI.</returns>
        <JsonPropertyName("ai_gender")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property AiGender As String = Nothing

        ''' <summary>
        ''' The new backstory for the AI. If not provided, the AI's backstory will not be updated.
        ''' </summary>
        <JsonPropertyName("ai_backstory")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property AiBackstory As String = Nothing

        ''' <summary>
        ''' The new key memories for the AI. If not provided, the AI's key memories will not be updated.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the new key memories for the AI.</returns>
        <JsonPropertyName("ai_memory")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property AiMemory As String = Nothing

        ''' <summary>
        ''' The new response directive for the AI. If not provided, the AI's response directive will not be updated.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the new response directive for the AI.</returns>
        <JsonPropertyName("ai_directive")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property AiDirective As String = Nothing

        ''' <summary>
        ''' The new example message for the AI. If not provided, the AI's example message will not be updated.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the new example message for the AI.</returns>
        <JsonPropertyName("ai_example_message")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property AiExampleMessage As String = Nothing

        ''' <summary>
        ''' The new additional context for the AI. If not provided, the AI's additional context will not be updated.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the new additional context for the AI.</returns>
        <JsonPropertyName("ai_additional_context")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property AiAdditionalContext As String = Nothing

        ''' <summary>
        ''' The most current scene for the AI. If not provided, the AI's current scene will not be updated.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the most current scene for the AI.</returns>
        <JsonPropertyName("current_scene")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property CurrentScene As String = Nothing

        ''' <summary>
        ''' The new user's name for the AI to use when addressing the user. If not provided, the AI will not update the name it uses to address the user.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the new user's name for the AI to use when addressing the user.</returns>
        <JsonPropertyName("user_name")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property UserName As String = Nothing

        ''' <summary>
        ''' The new user's gender for the AI to use when addressing the user. If not provided, the AI will not update the gender it uses to address the user.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the new user's gender for the AI to use when addressing the user.</returns>
        <JsonPropertyName("user_gender")>
        <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
        Public Property UserGender As String = Nothing

    End Class

End Namespace

