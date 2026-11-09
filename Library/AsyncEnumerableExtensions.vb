Imports System.Runtime.CompilerServices
Imports System.Runtime.ExceptionServices
Imports System.Text
Imports System.Threading

''' <summary>
''' Helpers for consuming the library's <see cref="IAsyncEnumerable(Of T)"/> results from Visual Basic.
''' <para>
''' Visual Basic has no <c>Await ForEach</c>, and its compiler also rejects <c>Await</c> inside <c>Finally</c>
''' (error BC36943), so the usual manual <c>GetAsyncEnumerator</c> loop with a <c>Finally Await DisposeAsync()</c>
''' does not compile. These extension methods do that loop correctly for you. They work from any .NET language.
''' </para>
''' </summary>
Public Module AsyncEnumerableExtensions

    ''' <summary>
    ''' Runs <paramref name="action"/> for every item, disposing the enumerator (and so cancelling any in-flight HTTP request) afterwards.
    ''' </summary>
    ''' <typeparam name="T">Item type.</typeparam>
    ''' <param name="source">The sequence, for example from <see cref="KindroidClient.SendMessageStreamAsync"/>.</param>
    ''' <param name="action">Called synchronously for each item.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>A task that completes when the sequence ends. Errors from the server surface as <see cref="Exceptions.KindroidException"/>.</returns>
    <Extension>
    Public Function ForEachAsync(Of T)(source As IAsyncEnumerable(Of T), action As Action(Of T),
                                       Optional cancellationToken As CancellationToken = Nothing) As Task
        ArgumentNullException.ThrowIfNull(source)
        ArgumentNullException.ThrowIfNull(action)
        Return ForEachCoreAsync(source, Function(item)
                                            action(item)
                                            Return Task.CompletedTask
                                        End Function, cancellationToken)
    End Function

    ''' <summary>
    ''' Awaits <paramref name="handler"/> for every item, in order, disposing the enumerator afterwards.
    ''' </summary>
    ''' <typeparam name="T">Item type.</typeparam>
    ''' <param name="source">The sequence.</param>
    ''' <param name="handler">Called for each item; the next item is not requested until the returned task completes.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>A task that completes when the sequence ends.</returns>
    <Extension>
    Public Function ForEachAwaitAsync(Of T)(source As IAsyncEnumerable(Of T), handler As Func(Of T, Task),
                                            Optional cancellationToken As CancellationToken = Nothing) As Task
        ArgumentNullException.ThrowIfNull(source)
        ArgumentNullException.ThrowIfNull(handler)
        Return ForEachCoreAsync(source, handler, cancellationToken)
    End Function

    ''' <summary>
    ''' Reads a text stream to the end and returns the concatenated reply. Chunks are joined verbatim
    ''' (whitespace-only chunks are part of the reply).
    ''' </summary>
    ''' <param name="source">A streaming reply, for example from <see cref="KindroidClient.SendMessageStreamAsync"/>.</param>
    ''' <param name="cancellationToken">Optional cancellation token.</param>
    ''' <returns>The full text.</returns>
    <Extension>
    Public Async Function ReadAllTextAsync(source As IAsyncEnumerable(Of String),
                                           Optional cancellationToken As CancellationToken = Nothing) As Task(Of String)
        ArgumentNullException.ThrowIfNull(source)
        Dim sb As New StringBuilder()
        Await ForEachCoreAsync(source, Function(chunk)
                                           sb.Append(chunk)
                                           Return Task.CompletedTask
                                       End Function, cancellationToken).ConfigureAwait(False)
        Return sb.ToString()
    End Function

    <System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
                                                     Justification:="The exception is captured so the enumerator can be disposed with Await (not allowed in Finally in VB), then rethrown unchanged.")>
    Private Async Function ForEachCoreAsync(Of T)(source As IAsyncEnumerable(Of T), handler As Func(Of T, Task),
                                                  cancellationToken As CancellationToken) As Task
        Dim enumerator = source.GetAsyncEnumerator(cancellationToken)
        Dim failure As Exception = Nothing
        Try
            While Await enumerator.MoveNextAsync().ConfigureAwait(False)
                Await handler(enumerator.Current).ConfigureAwait(False)
            End While
        Catch ex As Exception
            failure = ex
        End Try
        Await enumerator.DisposeAsync().ConfigureAwait(False)
        If failure IsNot Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
    End Function

End Module
