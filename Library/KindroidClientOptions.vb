''' <summary>
''' Optional settings for <see cref="KindroidClient"/>.
''' </summary>
Public NotInheritable Class KindroidClientOptions

    Private _baseUrl As Uri = New Uri("https://api.kindroid.ai/v1")
    Private _timeout As TimeSpan = TimeSpan.FromMinutes(10)
    Private _maxRetries As Integer
    Private _retryDelay As TimeSpan = TimeSpan.FromSeconds(10)

    ''' <summary>
    ''' Base address of the API, without a trailing slash. Defaults to <c>https://api.kindroid.ai/v1</c>.
    ''' </summary>
    ''' <returns>The base <see cref="Uri"/>.</returns>
    Public Property BaseUrl As Uri
        Get
            Return _baseUrl
        End Get
        Set(value As Uri)
            ArgumentNullException.ThrowIfNull(value)
            _baseUrl = value
        End Set
    End Property

    ''' <summary>
    ''' Overall timeout of a single HTTP request. Replies can take several seconds to generate, so this defaults to
    ''' 10 minutes instead of <see cref="System.Net.Http.HttpClient"/>'s 100 seconds. Use
    ''' <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to disable it.
    ''' Only applied to the <see cref="System.Net.Http.HttpClient"/> that the library creates itself.
    ''' </summary>
    ''' <returns>The request timeout.</returns>
    Public Property Timeout As TimeSpan
        Get
            Return _timeout
        End Get
        Set(value As TimeSpan)
            If value <= TimeSpan.Zero AndAlso value <> System.Threading.Timeout.InfiniteTimeSpan Then
                Throw New ArgumentOutOfRangeException(NameOf(value), "Timeout must be positive or Timeout.InfiniteTimeSpan.")
            End If
            _timeout = value
        End Set
    End Property

    ''' <summary>
    ''' How many times a request rejected with <c>429 Too Many Requests</c> is retried automatically.
    ''' Defaults to <c>0</c> (never retry). A 429 means the request was not processed, so retrying is safe for every endpoint.
    ''' </summary>
    ''' <returns>The maximum number of retries.</returns>
    Public Property MaxRetries As Integer
        Get
            Return _maxRetries
        End Get
        Set(value As Integer)
            ArgumentOutOfRangeException.ThrowIfNegative(value)
            _maxRetries = value
        End Set
    End Property

    ''' <summary>
    ''' Delay before the first retry; it doubles on every further attempt. A <c>Retry-After</c> header, if the server
    ''' ever sends one, takes precedence. The server was observed to need minutes, not seconds, to lift a 429,
    ''' so keep <see cref="MaxRetries"/> small. Defaults to 10 seconds.
    ''' </summary>
    ''' <returns>The initial retry delay.</returns>
    Public Property RetryDelay As TimeSpan
        Get
            Return _retryDelay
        End Get
        Set(value As TimeSpan)
            If value < TimeSpan.Zero Then
                Throw New ArgumentOutOfRangeException(NameOf(value), "RetryDelay cannot be negative.")
            End If
            _retryDelay = value
        End Set
    End Property

End Class
