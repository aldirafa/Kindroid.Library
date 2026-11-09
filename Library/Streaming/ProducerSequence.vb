Imports System.Diagnostics.CodeAnalysis
Imports System.Threading
Imports System.Threading.Channels

Namespace Streaming

    ''' <summary>
    ''' A lazy <see cref="IAsyncEnumerable(Of T)"/> fed by a producer delegate through a <see cref="Channel"/>.
    ''' <para>
    ''' Visual Basic has no <c>Async Iterator</c> support, so this is the building block for every streaming API in the
    ''' library. Guarantees: <b>every</b> producer exception (HTTP failure, network error, cancellation) is delivered to
    ''' the consumer from <c>MoveNextAsync</c> instead of leaving it waiting forever; disposing the enumerator early
    ''' cancels the producer so the HTTP connection is released; each enumeration runs the producer once.
    ''' </para>
    ''' </summary>
    ''' <typeparam name="T">Element type.</typeparam>
    Friend NotInheritable Class ProducerSequence(Of T)
        Implements IAsyncEnumerable(Of T)

        Private ReadOnly _producer As Func(Of ChannelWriter(Of T), CancellationToken, Task)
        Private ReadOnly _defaultToken As CancellationToken

        ''' <summary>
        ''' Creates the sequence.
        ''' </summary>
        ''' <param name="producer">Writes items to the supplied writer. May throw; the exception is forwarded to the consumer.</param>
        ''' <param name="cancellationToken">Token supplied by the API caller; combined with the one from <c>WithCancellation</c>.</param>
        Friend Sub New(producer As Func(Of ChannelWriter(Of T), CancellationToken, Task), cancellationToken As CancellationToken)
            _producer = producer
            _defaultToken = cancellationToken
        End Sub

        <SuppressMessage("Design", "CA1031:Do not catch general exception types",
                         Justification:="The producer runs on a background task; every exception must be forwarded to the consumer through the channel.")>
        Public Function GetAsyncEnumerator(Optional cancellationToken As CancellationToken = Nothing) As IAsyncEnumerator(Of T) Implements IAsyncEnumerable(Of T).GetAsyncEnumerator
            Dim cts = CancellationTokenSource.CreateLinkedTokenSource(_defaultToken, cancellationToken)
            Dim token = cts.Token
            Dim ch = Channel.CreateUnbounded(Of T)(New UnboundedChannelOptions With {.SingleReader = True, .SingleWriter = True})
            Dim producer = _producer

            Task.Run(Async Function() As Task
                         Try
                             Await producer(ch.Writer, token).ConfigureAwait(False)
                             ch.Writer.TryComplete()
                         Catch ex As Exception
                             ch.Writer.TryComplete(ex)
                         End Try
                     End Function, CancellationToken.None)

            Return New Enumerator(ch.Reader.ReadAllAsync(token).GetAsyncEnumerator(token), cts)
        End Function

        Private NotInheritable Class Enumerator
            Implements IAsyncEnumerator(Of T)

            Private ReadOnly _inner As IAsyncEnumerator(Of T)
            Private ReadOnly _cts As CancellationTokenSource

            Friend Sub New(inner As IAsyncEnumerator(Of T), cts As CancellationTokenSource)
                _inner = inner
                _cts = cts
            End Sub

            Public ReadOnly Property Current As T Implements IAsyncEnumerator(Of T).Current
                Get
                    Return _inner.Current
                End Get
            End Property

            Public Function MoveNextAsync() As ValueTask(Of Boolean) Implements IAsyncEnumerator(Of T).MoveNextAsync
                Return _inner.MoveNextAsync()
            End Function

            Public Function DisposeAsync() As ValueTask Implements IAsyncDisposable.DisposeAsync
                Return New ValueTask(DisposeCoreAsync())
            End Function

            Private Async Function DisposeCoreAsync() As Task
                Await _cts.CancelAsync().ConfigureAwait(False) ' stops the producer (and its HTTP request) if the consumer quit early
                Await _inner.DisposeAsync().ConfigureAwait(False)
                _cts.Dispose()
            End Function
        End Class

    End Class

End Namespace
