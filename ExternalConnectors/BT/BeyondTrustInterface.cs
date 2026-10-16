using Microsoft.Win32;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ExternalConnectors.BT;

/// <summary>
/// BeyondTrust Password Safe / BeyondInsight public API v3.
///
/// Two capabilities are exposed:
///  * <see cref="ListManagedAccounts"/> enumerates every requestable managed account so
///    they can be mirrored into the connection tree.
///  * <see cref="FetchSecretFromServer"/> releases the password of a single account at
///    connect time (POST Requests -> GET Credentials), the same contract the other
///    external credential providers use.
///
/// Released requests must be handed back with <see cref="CheckIn"/> once the connection
/// closes, otherwise the release stays open in the Password Safe audit trail.
/// </summary>
public class BeyondTrustInterface
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // accountId -> requestId of the release currently held for that account
    private static readonly Dictionary<int, int> ActiveRequests = [];

    // result of the last enumeration, used to resolve an account at connect time
    private static readonly Dictionary<(int systemId, int accountId), BTManagedAccount> AccountCache = [];

    private static class BTConnectionData
    {
        public static string url = "";
        public static string apiKey = "";
        public static string runAs = "";
        public static string runAsPassword = "";
        public static string challenge = "";
        public static bool initdone;

        private static HttpClient? client;
        private static bool signedIn;

        /// <summary>Base address of the API, e.g. https://ps.domain.local/BeyondTrust/api/public/v3</summary>
        public static string ApiBase
        {
            get
            {
                string u = url.TrimEnd('/');
                if (u.Contains("/api/public/", StringComparison.OrdinalIgnoreCase))
                    return u;
                return u + "/api/public/v3";
            }
        }

        /// <summary>
        /// The API keeps server side session state, so one client with one cookie
        /// container is kept for the lifetime of the process.
        /// </summary>
        public static HttpClient Client
        {
            get
            {
                if (client != null)
                    return client;

                HttpClientHandler handler = new()
                {
                    CookieContainer = new CookieContainer(),
                    UseCookies = true
                };
                client = new HttpClient(handler);
                client.DefaultRequestHeaders.Add("User-Agent", "mRemote");
                return client;
            }
        }

        public static string AuthorizationHeader()
        {
            StringBuilder sb = new();
            sb.Append("PS-Auth key=").Append(apiKey).Append("; runas=").Append(runAs).Append(';');
            if (!string.IsNullOrEmpty(runAsPassword))
                sb.Append(" pwd=[").Append(runAsPassword).Append("];");
            if (!string.IsNullOrEmpty(challenge))
                sb.Append(" challenge=").Append(challenge).Append(';');
            return sb.ToString();
        }

        public static void Reset()
        {
            signedIn = false;
            initdone = false;
            ActiveRequests.Clear();
            client?.Dispose();
            client = null;
        }

        /// <summary>
        /// Asks for connection data (once per process) and signs the application in.
        /// </summary>
        public static void Init()
        {
            if (initdone && signedIn)
                return;

            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\mRemoteBTInterface");
            try
            {
                BTConnectionForm f = new();

                string? storedUrl = key.GetValue("URL") as string;
                if (storedUrl == null || !storedUrl.Contains("://"))
                    storedUrl = "https://ps.domain.local/BeyondTrust";
                f.tbServerURL.Text = storedUrl;
                f.tbRunAs.Text = key.GetValue("RunAs") as string ?? "";
                f.tbAPIKey.Text = apiKey;   // may already be known from an earlier sign in

                while (true)
                {
                    _ = f.ShowDialog();

                    if (f.DialogResult != DialogResult.OK)
                        return;

                    url = f.tbServerURL.Text;
                    apiKey = f.tbAPIKey.Text;
                    runAs = f.tbRunAs.Text;
                    runAsPassword = f.tbRunAsPassword.Text;
                    challenge = f.tbChallenge.Text;

                    // a new sign in needs a fresh session
                    signedIn = false;
                    client?.Dispose();
                    client = null;

                    try
                    {
                        SignAppIn();
                        initdone = true;
                        signedIn = true;
                        break;
                    }
                    catch (BeyondTrustChallengeRequiredException ex)
                    {
                        MessageBox.Show(ex.Message, "BeyondTrust Password Safe");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Sign in failed: " + ex.Message, "BeyondTrust Password Safe");
                    }
                }

                key.SetValue("URL", url);
                key.SetValue("RunAs", runAs);
            }
            finally
            {
                key.Close();
            }
        }

        private static void SignAppIn()
        {
            using HttpRequestMessage request = new(HttpMethod.Post, $"{ApiBase}/Auth/SignAppIn");
            request.Headers.TryAddWithoutValidation("Authorization", AuthorizationHeader());

            HttpResponseMessage response = Client.Send(request);
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                response.Headers.TryGetValues("WWW-Authenticate-2FA", out IEnumerable<string>? prompts))
            {
                string prompt = string.Join(" ", prompts);
                throw new BeyondTrustChallengeRequiredException(
                    $"Two factor authentication required: {prompt}. Enter the code in the challenge field and sign in again.");
            }

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"POST Auth/SignAppIn returned {(int)response.StatusCode} {response.ReasonPhrase}: {ReadBody(response)}");
        }

        public static void SignOut()
        {
            if (!signedIn)
                return;

            try
            {
                using HttpRequestMessage request = new(HttpMethod.Post, $"{ApiBase}/Auth/Signout");
                request.Headers.TryAddWithoutValidation("Authorization", AuthorizationHeader());
                Client.Send(request);
            }
            catch (Exception)
            {
                // nothing useful to do while shutting down
            }
            finally
            {
                Reset();
            }
        }
    }

    #region HTTP helpers

    private static string ReadBody(HttpResponseMessage response)
    {
        try
        {
            return response.Content.ReadAsStringAsync().Result;
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static string Send(HttpMethod method, string relativeUrl, string? jsonBody = null)
    {
        using HttpRequestMessage request = new(method, $"{BTConnectionData.ApiBase}/{relativeUrl}");
        request.Headers.TryAddWithoutValidation("Authorization", BTConnectionData.AuthorizationHeader());
        if (jsonBody != null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        HttpResponseMessage response = BTConnectionData.Client.Send(request);
        string body = ReadBody(response);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"{method} {relativeUrl} returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");

        return body;
    }

    private static T? GetJson<T>(string relativeUrl)
    {
        return JsonSerializer.Deserialize<T>(Send(HttpMethod.Get, relativeUrl), JsonOptions);
    }

    #endregion

    #region enumeration

    private class ManagedSystem
    {
        public int ManagedSystemID { get; set; }
        public string? SystemName { get; set; }
        public string? HostName { get; set; }
        public string? DnsName { get; set; }
        public string? IPAddress { get; set; }
        public int? Port { get; set; }
        public int PlatformID { get; set; }
    }

    private class ManagedSystemContainer
    {
        public List<ManagedSystem>? Data { get; set; }
        public int TotalCount { get; set; }
    }

    private class Platform
    {
        public int PlatformID { get; set; }
        public string? Name { get; set; }
        public string? ShortName { get; set; }
        public bool PortFlag { get; set; }
        public int? DefaultPort { get; set; }
    }

    /// <summary>
    /// Returns every managed account the API user may request, with host, port and
    /// platform name resolved. Accounts without "Enable for API Access" are not
    /// returned by the API and therefore never show up here.
    /// </summary>
    public static IReadOnlyList<BTManagedAccount> ListManagedAccounts()
    {
        BTConnectionData.Init();
        if (!BTConnectionData.initdone)
            return [];

        List<BTManagedAccount> accounts = FetchAllManagedAccounts();
        Dictionary<int, ManagedSystem> systems = FetchAllManagedSystems();
        Dictionary<int, Platform> platforms = FetchPlatforms();

        foreach (BTManagedAccount account in accounts)
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

        lock (AccountCache)
        {
            AccountCache.Clear();
            foreach (BTManagedAccount account in accounts)
                AccountCache[(account.SystemId, account.AccountId)] = account;
        }

        return accounts;
    }

    /// <summary>
    /// Looks an account up in the result of the last enumeration, enumerating once
    /// if nothing has been fetched yet.
    /// </summary>
    private static BTManagedAccount? FindAccount(int systemId, int accountId)
    {
        lock (AccountCache)
        {
            if (AccountCache.TryGetValue((systemId, accountId), out BTManagedAccount? cached))
                return cached;
        }

        ListManagedAccounts();

        lock (AccountCache)
            return AccountCache.GetValueOrDefault((systemId, accountId));
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

    private static List<BTManagedAccount> FetchAllManagedAccounts()
    {
        const int pageSize = 1000;
        List<BTManagedAccount> all = [];

        for (int offset = 0; ; offset += pageSize)
        {
            List<BTManagedAccount>? page =
                GetJson<List<BTManagedAccount>>($"ManagedAccounts?limit={pageSize}&offset={offset}");

            if (page == null || page.Count == 0)
                break;

            all.AddRange(page);

            if (page.Count < pageSize)
                break;
        }

        return all;
    }

    private static Dictionary<int, ManagedSystem> FetchAllManagedSystems()
    {
        const int pageSize = 1000;
        Dictionary<int, ManagedSystem> all = [];

        for (int offset = 0; ; offset += pageSize)
        {
            // passing limit switches the response to the paged container shape
            ManagedSystemContainer? page =
                GetJson<ManagedSystemContainer>($"ManagedSystems?limit={pageSize}&offset={offset}");

            if (page?.Data == null || page.Data.Count == 0)
                break;

            foreach (ManagedSystem system in page.Data)
                all[system.ManagedSystemID] = system;

            if (page.Data.Count < pageSize)
                break;
        }

        return all;
    }

    private static Dictionary<int, Platform> FetchPlatforms()
    {
        Dictionary<int, Platform> platforms = [];

        try
        {
            List<Platform>? list = GetJson<List<Platform>>("Platforms");
            if (list == null)
                return platforms;

            foreach (Platform platform in list)
                platforms[platform.PlatformID] = platform;
        }
        catch (Exception)
        {
            // platform names are cosmetic plus a port fallback, so a failure here
            // must not break the whole enumeration
        }

        return platforms;
    }

    #endregion

    #region credential release

    private class RequestAddModel
    {
        public string AccessType { get; set; } = "View";
        public int SystemID { get; set; }
        public int AccountID { get; set; }
        public int DurationMinutes { get; set; }
        public string Reason { get; set; } = "";
        public string ConflictOption { get; set; } = "reuse";
    }

    /// <summary>
    /// Releases the password of one account. <paramref name="input"/> is the
    /// UserViaAPI value in "systemId:accountId" form, as produced by
    /// <see cref="BTManagedAccount.SecretId"/>.
    /// </summary>
    public static void FetchSecretFromServer(string input, out string username, out string password, out string domain, out string privatekey)
    {
        username = "";
        password = "";
        domain = "";
        privatekey = "";

        (int systemId, int accountId) = ParseSecretId(input);

        BTConnectionData.Init();
        if (!BTConnectionData.initdone)
            return;

        // the account name, domain and allowed release duration come from the
        // enumeration; the API has no "get requestable account by id" endpoint
        BTManagedAccount? account = FindAccount(systemId, accountId);
        if (account == null)
            throw new InvalidOperationException(
                $"Managed account {accountId} on system {systemId} was not found or is not requestable.");

        int duration = account.DefaultReleaseDuration ?? 0;
        if (duration <= 0)
            duration = account.MaximumReleaseDuration > 0 ? Math.Min(60, account.MaximumReleaseDuration) : 60;

        RequestAddModel requestModel = new()
        {
            AccessType = "View",
            SystemID = systemId,
            AccountID = accountId,
            DurationMinutes = duration,
            Reason = "mRemoteNG connection",
            ConflictOption = "reuse"
        };

        string requestIdBody = Send(HttpMethod.Post, "Requests", JsonSerializer.Serialize(requestModel));
        if (!int.TryParse(requestIdBody.Trim().Trim('"'), out int requestId))
            throw new InvalidOperationException($"POST Requests returned an unexpected body: {requestIdBody}");

        lock (ActiveRequests)
            ActiveRequests[accountId] = requestId;

        // the credential comes back as a bare JSON string
        password = Send(HttpMethod.Get, $"Credentials/{requestId}").Trim().Trim('"');
        username = account.AccountName;
        domain = account.DomainName ?? "";
    }

    /// <summary>
    /// Checks the release held for this account back in. Safe to call when nothing
    /// is held. <paramref name="input"/> uses the same "systemId:accountId" form.
    /// </summary>
    public static void CheckIn(string input)
    {
        (_, int accountId) = ParseSecretId(input);

        int requestId;
        lock (ActiveRequests)
        {
            if (!ActiveRequests.TryGetValue(accountId, out requestId))
                return;
            ActiveRequests.Remove(accountId);
        }

        Send(HttpMethod.Put, $"Requests/{requestId}/Checkin", "{}");
    }

    /// <summary>Checks in everything still held and ends the API session.</summary>
    public static void SignOut()
    {
        int[] requestIds;
        lock (ActiveRequests)
        {
            requestIds = [.. ActiveRequests.Values];
            ActiveRequests.Clear();
        }

        foreach (int requestId in requestIds)
        {
            try
            {
                Send(HttpMethod.Put, $"Requests/{requestId}/Checkin", "{}");
            }
            catch (Exception)
            {
                // best effort during shutdown
            }
        }

        BTConnectionData.SignOut();
    }

    private static (int systemId, int accountId) ParseSecretId(string input)
    {
        string[] parts = (input ?? "").Split(':', StringSplitOptions.TrimEntries);

        if (parts.Length == 2 && int.TryParse(parts[0], out int systemId) && int.TryParse(parts[1], out int accountId))
            return (systemId, accountId);

        throw new FormatException(
            $"'{input}' is not a valid BeyondTrust account identifier. Expected 'systemId:accountId'.");
    }

    #endregion
}

/// <summary>Thrown when Password Safe asks for a second factor.</summary>
public class BeyondTrustChallengeRequiredException(string message) : Exception(message);
