using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BlinkyAccess.Client;

/// <summary>
/// Client for the Blinky Access connections API.
///
/// One call, GET /api/user/v1/connections, returns every connection the token's user
/// may open. The token is sent in the Authorization header only; it never appears in a
/// URL, in a log line or in an exception message, and the response body is never
/// written anywhere.
///
/// Certificate validation is always on: there is deliberately no option to ignore it.
/// </summary>
public sealed class BlinkyAccessClient : IDisposable
{
    /// <summary>The only response format this version understands.</summary>
    public const string SupportedFormat = "blinky-access-connections/1";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Shared across the application, as a socket exhausting new client per sync would
    /// otherwise be created. Its own timeout is generous; each call enforces
    /// <see cref="BlinkyAccessOptions.Timeout"/> with a cancellation token instead.
    /// </summary>
    private static readonly Lazy<HttpClient> SharedClient = new(() => new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(2)
    });

    private readonly BlinkyAccessOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    /// <param name="handler">
    /// Replaced in tests. When null the shared client is used, which is what a desktop
    /// application wants; a handler of its own is disposed with this instance.
    /// </param>
    public BlinkyAccessClient(BlinkyAccessOptions options, HttpMessageHandler? handler = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (handler == null)
        {
            _httpClient = SharedClient.Value;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient(handler);
            _ownsHttpClient = true;
        }
    }

    /// <summary>
    /// Reads every connection the token's user may open.
    /// </summary>
    /// <exception cref="BlinkyAccessConfigurationException">
    /// The portal address or the token is missing or unusable; nothing was contacted.
    /// </exception>
    /// <exception cref="BlinkyAccessUnauthorizedException">The token was refused.</exception>
    /// <exception cref="BlinkyAccessThrottledException">The portal is rate limiting.</exception>
    /// <exception cref="BlinkyAccessUnsupportedFormatException">
    /// The portal answered in a format this version does not understand.
    /// </exception>
    /// <exception cref="BlinkyAccessException">Any other failure of the call.</exception>
    public IReadOnlyList<BlinkyAccessConnection> GetConnections()
    {
        return ParseConnectionsResponse(GetConnectionsBody());
    }

    /// <summary>
    /// Makes the one call the synchronisation depends on and reports it in a single
    /// line, so a failing setup can be told apart without reading a stack trace:
    /// wrong address, refused token, rate limit, untrusted certificate or an
    /// unsupported portal version.
    ///
    /// Never throws, and never reports the token.
    /// </summary>
    public BlinkyAccessTestResult TestConnection()
    {
        try
        {
            IReadOnlyList<BlinkyAccessConnection> connections = GetConnections();

            return new BlinkyAccessTestResult
            {
                Success = true,
                ConnectionCount = connections.Count,
                Detail = connections.Count == 0
                    ? "Connected, but the portal returned no connections for this user."
                    : $"Connected; {connections.Count} connection(s) available."
            };
        }
        catch (Exception ex)
        {
            return new BlinkyAccessTestResult { Success = false, Detail = Explain(ex) };
        }
    }

    /// <summary>
    /// Turns a response body into connections, checking the format first. Separate from
    /// the call itself so the wire format can be covered without an HTTP stub.
    /// </summary>
    public static IReadOnlyList<BlinkyAccessConnection> ParseConnectionsResponse(string body)
    {
        BlinkyAccessConnectionsResponse? response;

        try
        {
            response = JsonSerializer.Deserialize<BlinkyAccessConnectionsResponse>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new BlinkyAccessException("The portal did not answer with valid JSON.", ex);
        }

        if (response == null)
            throw new BlinkyAccessException("The portal answered with an empty body.");

        if (!string.Equals(response.Format, SupportedFormat, StringComparison.Ordinal))
            throw new BlinkyAccessUnsupportedFormatException(response.Format);

        return response.Connections ?? [];
    }

    /// <summary>Turns the usual failures into something actionable.</summary>
    public static string Explain(Exception exception)
    {
        switch (exception)
        {
            case BlinkyAccessUnauthorizedException:
                return "The portal rejected the access token; it is wrong, expired or revoked. Issue a new token in the portal.";

            case BlinkyAccessThrottledException throttled:
                return throttled.RetryAfter.HasValue
                    ? $"The portal is rate limiting this client; try again in {(int)throttled.RetryAfter.Value.TotalSeconds} second(s)."
                    : "The portal is rate limiting this client; try again later.";

            case BlinkyAccessUnsupportedFormatException format:
                return $"This version of mRemoteNG does not support the portal response format '{format.ReportedFormat}'.";

            case BlinkyAccessException api:
                return api.Message;

            case TaskCanceledException:
            case OperationCanceledException:
                return "The call timed out; the address may be wrong or blocked by a firewall.";
        }

        for (Exception? inner = exception; inner != null; inner = inner.InnerException)
        {
            if (inner is System.Security.Authentication.AuthenticationException)
                return "TLS failed; the portal certificate is not trusted by this machine.";

            if (inner is System.Net.Sockets.SocketException socket)
                return $"Network error ({socket.SocketErrorCode}); check the address and that the portal is reachable.";
        }

        return exception.Message;
    }

    private string GetConnectionsBody()
    {
        string token = (_options.Token ?? "").Trim();

        if (token.Length == 0)
            throw new BlinkyAccessConfigurationException("No access token is configured.");

        Uri url = _options.ConnectionsUrl;

        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd(_options.UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using CancellationTokenSource timeout = new(_options.Timeout);
        using HttpResponseMessage response = Send(request, timeout.Token);

        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                throw new BlinkyAccessUnauthorizedException();

            case HttpStatusCode.TooManyRequests:
                throw new BlinkyAccessThrottledException(response.Headers.RetryAfter?.Delta);
        }

        if (!response.IsSuccessStatusCode)
        {
            // the body is deliberately left out: it is neither needed nor ours to keep
            throw new BlinkyAccessException(
                $"GET {url.AbsolutePath} failed with {(int)response.StatusCode} {response.ReasonPhrase}.",
                response.StatusCode);
        }

        return response.Content.ReadAsStringAsync(timeout.Token).GetAwaiter().GetResult();
    }

    private HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return _httpClient.Send(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new BlinkyAccessException(Explain(ex), ex);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
