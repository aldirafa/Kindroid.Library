Imports System.IO
Imports System.Text
Imports System.Threading

Namespace Streaming

    ''' <summary>
    ''' Helpers for reading the raw-text streaming responses of the Kindroid API.
    ''' <para>
    ''' Observed behaviour: the body is <c>text/event-stream</c> but contains plain text (no SSE framing). Chunks are
    ''' arbitrary token fragments, and whitespace-only chunks (even a lone space) are part of the reply, so nothing may
    ''' be filtered out. Multi-byte characters can be split between reads, hence the stateful <see cref="StreamReader"/>.
    ''' </para>
    ''' </summary>
    Friend Module StreamingHelper

        ''' <summary>Size of the character buffer used for each read.</summary>
        Friend Const BufferSize As Integer = 4096

        ''' <summary>
        ''' Creates a UTF-8 reader over the response stream that correctly handles characters split across reads.
        ''' </summary>
        ''' <param name="stream">The response stream.</param>
        ''' <returns>A <see cref="StreamReader"/> that leaves no BOM detection side effects.</returns>
        Friend Function CreateReader(stream As Stream) As StreamReader
            Return New StreamReader(stream, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False), detectEncodingFromByteOrderMarks:=False, bufferSize:=1024, leaveOpen:=False)
        End Function

        ''' <summary>
        ''' Reads the next chunk of text, including whitespace-only chunks.
        ''' </summary>
        ''' <param name="reader">The reader created by <see cref="CreateReader(Stream)"/>.</param>
        ''' <param name="buffer">Scratch buffer of <see cref="BufferSize"/> characters.</param>
        ''' <param name="cancellationToken">Token to cancel the read.</param>
        ''' <returns>The next chunk, or <see langword="Nothing"/> at the end of the stream.</returns>
        Friend Async Function ReadChunkAsync(reader As StreamReader, buffer As Char(), cancellationToken As CancellationToken) As Task(Of String)
            Dim count = Await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(False)
            If count = 0 Then Return Nothing
            Return New String(buffer, 0, count)
        End Function

    End Module

End Namespace
