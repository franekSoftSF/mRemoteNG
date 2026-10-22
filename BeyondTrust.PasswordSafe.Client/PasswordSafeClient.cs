using System.Net;
using System.Text;
using System.Text.Json;

namespace BeyondTrust.PasswordSafe.Client;

/// <summary>
/// Client for the BeyondInsight and Password Safe public API v3.
///
/// Covers the credential checkout flow: sign in, enumerate the accounts the user may
/// request, create a release request, read the credential, hand it back.
///
/// The API keeps server side session state, so one instance owns one session and one
/// cookie container. It is not thread safe for sign in and sign out; the read and
/// release calls may be used from several threads once signed in.
/// </summary>
public sealed class PasswordSafeClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const int PageSize = 1000;

    private readonly PasswordSafeOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    /// <param name="handler">
    /// Replaced in tests; when null a handler with its own cookie container is used,
    /// which is what the session state of the API needs.
    /// </param>
    public PasswordSafeClient(PasswordSafeOptions options, HttpMessageHandler? handler = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _httpClient = new HttpClient(handler ?? new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true
        });
        _httpClient.Timeout = options.Timeout;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
        _ownsHttpClient = true;
    }

    public bool IsSignedIn { get; private set; }

    #region session

    /// <summary>
    /// Starts an API session.
    /// </summary>
    /// <exception cref="PasswordSafeChallengeRequiredException">
    /// A second factor is configured; fill in <see cref="PasswordSafeOptions.Challenge"/>
    /// and call again.
    /// </exception>
    public void SignIn()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"{_options.ApiBase}/Auth/SignAppIn");
        request.Headers.TryAddWithoutValidation("Authorization", BuildAuthorizationHeader());

        using HttpResponseMessage response = _httpClient.Send(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            response.Headers.TryGetValues("WWW-Authenticate-2FA", out IEnumerable<string>? prompts))
        {
            throw new PasswordSafeChallengeRequiredException(string.Join(" ", prompts));
        }

        if (!response.IsSuccessStatusCode)
        {
            string body = ReadBody(response);
            throw new PasswordSafeException(
                $"Sign in failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}",
                response.StatusCode, body);
        }

        IsSignedIn = true;
    }

    /// <summary>Ends the API session. Never throws.</summary>
    public void SignOut()
    {
        if (!IsSignedIn)
            return;

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, $"{_options.ApiBase}/Auth/Signout");
            request.Headers.TryAddWithoutValidation("Authorization", BuildAuthorizationHeader());
            using HttpResponseMessage response = _httpClient.Send(request);
        }
        catch (Exception)
        {
            // the session expires on its own; nothing useful to do here
        }
        finally
        {
            IsSignedIn = false;
        }
    }

    private string BuildAuthorizationHeader()
    {
        StringBuilder header = new();
        header.Append("PS-Auth key=").Append(_options.ApiKey).Append("; runas=").Append(_options.RunAsUser).Append(';');

        if (!string.IsNullOrEmpty(_options.RunAsPassword))
            header.Append(" pwd=[").Append(_options.RunAsPassword).Append("];");

        if (!string.IsNullOrEmpty(_options.Challenge))
            header.Append(" challenge=").Append(_options.Challenge).Append(';');

        return header.ToString();
    }

    #endregion

    #region diagnostics

    /// <summary>
    /// Walks the calls the application depends on and reports each one, so a failing
    /// setup can be told apart without reading a stack trace: wrong address, refused
    /// key, missing permission or an account list the user cannot see.
    ///
    /// Never throws, and never reports a credential.
    /// </summary>
    public ConnectionTestResult TestConnection()
    {
        ConnectionTestResult result = new();

        result.Steps.Add(RunStep("Sign in", $"POST {_options.ApiBase}/Auth/SignAppIn", () =>
        {
            SignIn();
            return $"signed in as {_options.RunAsUser}";
        }));

        if (!result.Success)
            return result;

        result.Steps.Add(RunStep("Requestable accounts", "GET ManagedAccounts", () =>
        {
            List<ManagedAccount>? accounts = GetJson<List<ManagedAccount>>("ManagedAccounts?limit=1000&offset=0");
            int count = accounts?.Count ?? 0;

            return count == 0
                ? "none returned; check that the accounts have \"Enable for API Access\" and that the Run As user has the Requestor role"
                : $"{count} account(s) visible, for example {accounts![0].SystemName}\\{accounts[0].AccountName}";
        }));

        result.Steps.Add(RunStep("Managed systems", "GET ManagedSystems", () =>
        {
            ManagedSystemContainer? systems = GetJson<ManagedSystemContainer>("ManagedSystems?limit=1&offset=0");
            return $"{systems?.TotalCount ?? 0} system(s); host names and ports come from here";
        }));

        result.Steps.Add(RunStep("Platforms", "GET Platforms", () =>
        {
            List<Platform>? platforms = GetJson<List<Platform>>("Platforms");
            return $"{platforms?.Count ?? 0} platform(s); used to pick RDP or SSH";
        }));

        return result;
    }

    private static ConnectionTestStep RunStep(string name, string call, Func<string> action)
    {
        try
        {
            return new ConnectionTestStep(name, true, action());
        }
        catch (Exception ex)
        {
            return new ConnectionTestStep(name, false, $"{call} - {Explain(ex)}");
        }
    }

    /// <summary>Turns the usual failures into something actionable.</summary>
    private static string Explain(Exception exception)
    {
        switch (exception)
        {
            case PasswordSafeChallengeRequiredException challenge:
                return $"two factor authentication required ({challenge.Prompt}); fill in the challenge field";

            case PasswordSafeAccessDeniedException denied:
                return $"access denied ({(int?)denied.StatusCode}); the API key, the Run As user or its roles do not allow this call";

            case PasswordSafeException api:
                return api.Message;

            case TaskCanceledException:
                return "timed out; the address may be wrong or blocked by a firewall";
        }

        for (Exception? inner = exception; inner != null; inner = inner.InnerException)
        {
            if (inner is System.Security.Authentication.AuthenticationException)
                return "TLS failed; the server certificate is not trusted by this machine";

            if (inner is System.Net.Sockets.SocketException socket)
                return $"network error ({socket.SocketErrorCode}); check the address and that the host is reachable";
        }

        return exception.Message;
    }

    #endregion

    #region enumeration

    /// <summary>
    /// Every managed account the signed in user may request, with host, port and
    /// platform resolved from the managed systems and platforms.
    ///
    /// Accounts without "Enable for API Access" are not returned by the API, so they
    /// never appear here either.
    /// </summary>
    public IReadOnlyList<ManagedAccount> ListManagedAccounts()
    {
        List<ManagedAccount> accounts = FetchAllPages<ManagedAccount>("ManagedAccounts");
        Dictionary<int, ManagedSystem> systems = FetchManagedSystems();
        Dictionary<int, Platform> platforms = FetchPlatforms();

        foreach (ManagedAccount account in accounts)
        {
            platforms.TryGetValue(account.PlatformID, out Platform? platform);
            account.PlatformName = platform?.Name ?? platform?.ShortName ?? "";

            if (!systems.TryGetValue(account.SystemId, out ManagedSystem? system))
            {
                account.Host = account.SystemName;
                continue;
            }

            account.Host = FirstNonEmpty(system.DnsName, system.HostName, system.IPAddress, system.SystemName, account.SystemName);
            account.Port = system.Port ?? (platform?.PortFlag == true ? platform.DefaultPort : null);
        }

        return accounts;
    }

    private List<T> FetchAllPages<T>(string resource)
    {
        List<T> all = [];

        for (int offset = 0; ; offset += PageSize)
        {
            List<T>? page = GetJson<List<T>>($"{resource}?limit={PageSize}&offset={offset}");

            if (page == null || page.Count == 0)
                break;

            all.AddRange(page);

            if (page.Count < PageSize)
                break;
        }

        return all;
    }

    private Dictionary<int, ManagedSystem> FetchManagedSystems()
    {
        Dictionary<int, ManagedSystem> all = [];

        for (int offset = 0; ; offset += PageSize)
        {
            // passing limit switches the response to the paged container shape
            ManagedSystemContainer? page = GetJson<ManagedSystemContainer>($"ManagedSystems?limit={PageSize}&offset={offset}");

            if (page?.Data == null || page.Data.Count == 0)
                break;

            foreach (ManagedSystem system in page.Data)
                all[system.ManagedSystemID] = system;

            if (page.Data.Count < PageSize)
                break;
        }

        return all;
    }

    private Dictionary<int, Platform> FetchPlatforms()
    {
        Dictionary<int, Platform> platforms = [];

        try
        {
            foreach (Platform platform in GetJson<List<Platform>>("Platforms") ?? [])
                platforms[platform.PlatformID] = platform;
        }
        catch (PasswordSafeException)
        {
            // platform data only supplies a display name and a port fallback, so
            // losing it must not cost the whole enumeration
        }

        return platforms;
    }

    private static string FirstNonEmpty(params string?[] candidates)
    {
        foreach (string? candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return "";
    }

    #endregion

    #region release

    /// <summary>
    /// Creates a release request and returns its id. With the default conflict option
    /// an active request of the same user for the same account is reused rather than
    /// a second one being opened.
    /// </summary>
    public int CreateRequest(int systemId, int accountId, int durationMinutes,
                             string reason = "", string accessType = "View", string conflictOption = "reuse")
    {
        RequestAddModel model = new()
        {
            AccessType = accessType,
            SystemID = systemId,
            AccountID = accountId,
            DurationMinutes = durationMinutes,
            Reason = reason,
            ConflictOption = conflictOption
        };

        string body = Send(HttpMethod.Post, "Requests", JsonSerializer.Serialize(model));

        if (!int.TryParse(body.Trim().Trim('"'), out int requestId))
            throw new PasswordSafeException($"Creating a request returned an unexpected body: {body}");

        return requestId;
    }

    /// <summary>
    /// Reads the credential of an approved request. The response body is the secret,
    /// so it is never put into an exception or returned on failure.
    /// </summary>
    public string GetCredential(int requestId)
    {
        return Send(HttpMethod.Get, $"Credentials/{requestId}", includeBodyInErrors: false).Trim().Trim('"');
    }

    /// <summary>Hands a release back, which ends it in the audit trail.</summary>
    public void CheckIn(int requestId, string reason = "")
    {
        Send(HttpMethod.Put, $"Requests/{requestId}/Checkin", JsonSerializer.Serialize(new { Reason = reason }));
    }

    /// <summary>
    /// Requests and reads the credential of one account in a single step, the shape
    /// a connecting application needs.
    /// </summary>
    public ReleasedCredential Release(ManagedAccount account, string reason = "")
    {
        ArgumentNullException.ThrowIfNull(account);

        int requestId = CreateRequest(account.SystemId, account.AccountId, account.ReleaseDurationMinutes, reason);

        return new ReleasedCredential
        {
            RequestId = requestId,
            Username = account.AccountName,
            Password = GetCredential(requestId),
            Domain = account.DomainName ?? ""
        };
    }

    #endregion

    #region http

    private T? GetJson<T>(string relativeUrl)
    {
        return JsonSerializer.Deserialize<T>(Send(HttpMethod.Get, relativeUrl), JsonOptions);
    }

    private string Send(HttpMethod method, string relativeUrl, string? jsonBody = null, bool includeBodyInErrors = true)
    {
        using HttpRequestMessage request = new(method, $"{_options.ApiBase}/{relativeUrl}");
        request.Headers.TryAddWithoutValidation("Authorization", BuildAuthorizationHeader());

        if (jsonBody != null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = _httpClient.Send(request);
        string body = ReadBody(response);

        if (response.IsSuccessStatusCode)
            return body;

        string detail = includeBodyInErrors ? $": {body}" : "";
        string message = $"{method} {relativeUrl} failed with {(int)response.StatusCode} {response.ReasonPhrase}{detail}";

        throw response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized
            ? new PasswordSafeAccessDeniedException(message, response.StatusCode, includeBodyInErrors ? body : null)
            : new PasswordSafeException(message, response.StatusCode, includeBodyInErrors ? body : null);
    }

    private static string ReadBody(HttpResponseMessage response)
    {
        try
        {
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            return "";
        }
    }

    #endregion

    public void Dispose()
    {
        SignOut();

        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    #region wire models

    private sealed class RequestAddModel
    {
        public string AccessType { get; set; } = "View";
        public int SystemID { get; set; }
        public int AccountID { get; set; }
        public int DurationMinutes { get; set; }
        public string Reason { get; set; } = "";
        public string ConflictOption { get; set; } = "reuse";
    }

    private sealed class ManagedSystem
    {
        public int ManagedSystemID { get; set; }
        public string? SystemName { get; set; }
        public string? HostName { get; set; }
        public string? DnsName { get; set; }
        public string? IPAddress { get; set; }
        public int? Port { get; set; }
        public int PlatformID { get; set; }
    }

    private sealed class ManagedSystemContainer
    {
        public List<ManagedSystem>? Data { get; set; }
        public int TotalCount { get; set; }
    }

    private sealed class Platform
    {
        public int PlatformID { get; set; }
        public string? Name { get; set; }
        public string? ShortName { get; set; }
        public bool PortFlag { get; set; }
        public int? DefaultPort { get; set; }
    }

    #endregion
}
