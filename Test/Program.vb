Imports Microsoft.Extensions.Configuration
Imports Spectre.Console
Imports Kindroid.Library
Imports System.Text
Imports System.Threading
Imports System.Net.Security

Module Program

    Sub Main(args As String())
        ' Sub Main TETAP sync. Kalau dijadiin "Async Sub Main", proses bisa kepotong
        ' duluan karena Async Sub itu "fire and forget" - gak ditunggu sampe selesai.
        ' Jadi kita panggil MainAsync lewat GetAwaiter().GetResult() biar bener2 di-block
        ' sampe semua proses async-nya kelar.
        MainAsync(args).GetAwaiter().GetResult()
    End Sub

    Private Async Function MainAsync(args As String()) As Task

        System.Console.OutputEncoding = System.Text.Encoding.UTF8
        System.Console.InputEncoding = System.Text.Encoding.UTF8

        ' ====== API Key ===================================
        Dim builder As New ConfigurationBuilder()
        builder.AddUserSecrets("27cceab5-9467-42a3-beae-5713e765703f")
        Dim config As IConfiguration = builder.Build()
        Dim apiKey As String = config("Kindroid:API_KEY")

        ' ====== Setup Client ==============================
        ' args = aiId
        Dim aiId As String = "g4f5wrgPRhkA4B8M0AzH"
        Dim aiName As String = "Mike"
        If args.Length = 1 Then
            aiId = args(0)
        End If
        ' get kindroid name
        If args.Length = 2 Then
            aiId = args(0)
            aiName = args(1)
        End If

        Dim kindroidClient As New KindroidClient(apiKey)

        AnsiConsole.MarkupLine("[bold green]Kindroid Streaming Test[/]")
        AnsiConsole.MarkupLine("[grey]Ketik pesan kamu, atau ketik 'exit' buat keluar.[/]")
        AnsiConsole.WriteLine()

        ' ====== Loop Chat Berkelanjutan ====================
        While True
            Dim message As String = AnsiConsole.Ask(Of String)("[cyan]Kamu:[/]")

            If String.IsNullOrWhiteSpace(message) Then
                Continue While
            End If

            If message.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase) Then
                Exit While
            End If

            AnsiConsole.WriteLine()
            Await StreamReplyAsync(kindroidClient, aiId, message, aiName)
            AnsiConsole.WriteLine()
        End While

        AnsiConsole.MarkupLine("[grey]Sampai jumpa, bestie! 👋[/]")

    End Function

    Private Function ReplaceKindroidMarkup(t As String) As String
        ' *...* -> italic
        ' **...** -> bold
        Dim italicPattern As New RegularExpressions.Regex("\*(.+?)\*", RegularExpressions.RegexOptions.Singleline)
        Dim boldPattern As New RegularExpressions.Regex("\*\*(.+?)\*\*", RegularExpressions.RegexOptions.Singleline)

        ' replace all *...* to spectre.console italic
        ' replace all **...** to spectre.console bold
        ' then return marked up text for spectre console to simply display
        t = boldPattern.Replace(t, "[bold]$1[/]")
        t = italicPattern.Replace(t, "[italic]$1[/]")
        Return t
    End Function

    ''' <summary>
    ''' Ngirim satu pesan ke AI, lalu nge-stream balesannya secara live
    ''' ke dalam Panel Spectre.Console (update tiap chunk dateng).
    ''' </summary>
    Private Async Function StreamReplyAsync(client As KindroidClient, aiId As String, message As String, aiName As String) As Task

        Dim fullText As New StringBuilder()

        Using cts As New CancellationTokenSource()

            Dim initialPanel As New Panel(New Markup("[grey italic](menunggu balasan...)[/]")) With {
                .Header = New PanelHeader($"[bold magenta]{aiName}[/]"),
                .Border = BoxBorder.Rounded
            }

            Try
                Await AnsiConsole.Live(initialPanel).
                    StartAsync(Async Function(ctx As LiveDisplayContext) As Task

                                   ' VB.NET gak punya sintaks "await foreach" kayak C#, jadi kita
                                   ' ambil enumerator-nya manual, sama kayak pattern di SendMessageStreamAsync.
                                   Dim enumerator = client.SendMessageStreamAsync(aiId, message, cts.Token).GetAsyncEnumerator(cts.Token)
                                   While Await enumerator.MoveNextAsync()
                                       Dim chunk = enumerator.Current
                                       fullText.Append(chunk)

                                       ' Markup.Escape penting banget di sini -> teks balesan AI bisa
                                       ' aja ngandung karakter "[" / "]" yang nanti disalahartikan
                                       ' sebagai markup tag sama Spectre.Console kalau gak di-escape.
                                       Dim updatedPanel As New Panel(New Markup(ReplaceKindroidMarkup(Markup.Escape(fullText.ToString())))) With {
                                                                                .Header = New PanelHeader($"[bold magenta]{aiName}[/]"),
                                                                                .Border = BoxBorder.Rounded
                                                                            }

                                       ctx.UpdateTarget(updatedPanel)
                                       ctx.Refresh()
                                   End While
                                   Await enumerator.DisposeAsync()

                               End Function)

            Catch ex As Exception
                ' Full detail: type, message, dan stack trace - di-render rapi oleh Spectre.
                AnsiConsole.MarkupLine("[bold red]Streaming gagal![/]")
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths)
            End Try

        End Using

    End Function

End Module