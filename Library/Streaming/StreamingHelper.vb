Imports System.IO
Imports System.Net.Http
Imports System.Text
Imports System.Threading
Imports System.Threading.Channels

Namespace Streaming

    ''' <summary>
    ''' Provides helpers to consume a plain-text streaming HTTP response as an <see cref="IAsyncEnumerable(Of String)"/> of
    ''' text chunks.
    ''' </summary>
    Friend Module StreamingHelper

        ''' <summary>
        ''' Reads a streaming <see cref="HttpResponseMessage"/> and yields each non-empty text chunk as it arrives.
        ''' </summary>
        ''' <param name="response">A successful streaming response from the Kindroid API.</param>
        ''' <param name="cancellationToken">Token to cancel the enumeration.</param>
        ''' <returns>An async sequence of text chunks.</returns>
        Public Function ReadChunksAsync(response As HttpResponseMessage,
                                        Optional cancellationToken As CancellationToken = Nothing) As IAsyncEnumerable(Of String)

            Dim ch = Channel.CreateUnbounded(Of String)(New UnboundedChannelOptions With {
                                                        .SingleReader = True,
                                                        .SingleWriter = True})

            Task.Run(Async Function()
                         Try
                             Using stream As Stream = Await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(False)

                                 Using reader As New StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks:=False, bufferSize:=1024, leaveOpen:=True)

                                     Dim buffer(4095) As Char

                                     ' Initialize by reading the first chunk
                                     Dim charsRead As Integer = Await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(False)

                                     ' Loop while we have data
                                     While charsRead > 0
                                         Dim chunk = New String(buffer, 0, charsRead)
                                         If Not String.IsNullOrWhiteSpace(chunk) Then
                                             Await ch.Writer.WriteAsync(chunk, cancellationToken).ConfigureAwait(False)
                                         End If
                                         ' Read the next chunk
                                         charsRead = Await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(False)
                                     End While

                                 End Using

                             End Using
                         Catch ex As KindroidException
                             ch.Writer.TryComplete(ex)
                             Return
                         Finally
                             ch.Writer.Complete()
                         End Try

                     End Function, cancellationToken)

            Return ch.Reader.ReadAllAsync(cancellationToken)

        End Function
    End Module

End Namespace