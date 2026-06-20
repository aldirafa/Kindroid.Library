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
        ''' Type of the sender.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the type of the sender.</returns>
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
        ''' A list of URLs of images attached to/sent with the message.
        ''' </summary>
        ''' <returns>A list of strings representing the URLs of images attached to/sent with the message.</returns>
        <JsonPropertyName("image_urls")>
        Public Property ImageUrls As List(Of String) = New List(Of String)()

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
        Public Property LinkUrl As String = String.Empty

        ''' <summary>
        ''' If the message was sent with a URL attached, this property will contain the title of the webpage at the URL and/or a description of the link, if available.
        ''' </summary>
        ''' <returns>A <see cref="String"/> representing the title of the webpage at the URL and/or a description of the link, if available.</returns>
        <JsonPropertyName("link_description")>
        Public Property LinkDescription As String = String.Empty

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
        ''' The timestamp of the last message in the current page.
        ''' </summary>
        ''' <returns>A long representing the Unix timestamp of the last message in the current page.</returns>
        <JsonPropertyName("lastTimestamp")>
        Public Property LastTimestamp As Long

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
        Public Property Messages As List(Of ChatMessage) = New List(Of ChatMessage)()

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

