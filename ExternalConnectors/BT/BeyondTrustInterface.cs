using BeyondTrust.PasswordSafe.Client;
using Microsoft.Win32;

namespace ExternalConnectors.BT;

/// <summary>
/// Adapter between mRemoteNG and <see cref="PasswordSafeClient"/>.
///
/// The API itself lives in the BeyondTrust.PasswordSafe.Client library, which knows
/// nothing about mRemoteNG. What is left here is what the application needs: asking
/// the user for connection data once per process, remembering the non secret parts,
/// and the same static entry points the other external credential providers have.
/// </summary>
public class BeyondTrustInterface
{
    private const string RegistryPath = @"SOFTWARE\mRemoteBTInterface";

    private static readonly object SyncRoot = new();

    // accountId -> requestId of the release currently held for that account
    private static readonly Dictionary<int, int> ActiveRequests = [];

    // result of the last enumeration, used to resolve an account at connect time
    private static readonly Dictionary<string, ManagedAccount> AccountCache = [];

    private static PasswordSafeClient? _client;
    private static string _apiKey = "";

    /// <summary>
    /// The signed in client, asking for connection data and signing in when needed.
    /// Returns null when the user dismissed the dialog.
    /// </summary>
    private static PasswordSafeClient? GetClient()
    {
        lock (SyncRoot)
        {
            if (_client is { IsSignedIn: true })
                return _client;

            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath);

            BTConnectionForm form = new();
            string? storedUrl = key.GetValue("URL") as string;
            form.tbServerURL.Text = storedUrl is null || !storedUrl.Contains("://")
                ? "https://ps.domain.local/BeyondTrust"
                : storedUrl;
            form.tbRunAs.Text = key.GetValue("RunAs") as string ?? "";
            form.tbAPIKey.Text = _apiKey;   // may already be known from an earlier sign in

            while (true)
            {
                if (form.ShowDialog() != DialogResult.OK)
                    return null;

                PasswordSafeOptions options = new()
                {
                    Url = form.tbServerURL.Text,
                    ApiKey = form.tbAPIKey.Text,
                    RunAsUser = form.tbRunAs.Text,
                    RunAsPassword = form.tbRunAsPassword.Text,
                    Challenge = form.tbChallenge.Text
                };

                _client?.Dispose();
                _client = new PasswordSafeClient(options);

                try
                {
                    _client.SignIn();
                    _apiKey = options.ApiKey;

                    // only the parts that are not secret are remembered
                    key.SetValue("URL", options.Url);
                    key.SetValue("RunAs", options.RunAsUser);

                    return _client;
                }
                catch (PasswordSafeChallengeRequiredException ex)
                {
                    MessageBox.Show($"{ex.Message}. Enter the code in the challenge field and sign in again.",
                        "BeyondTrust Password Safe");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Sign in failed: " + ex.Message, "BeyondTrust Password Safe");
                }
            }
        }
    }

    /// <summary>
    /// Every managed account the API user may request. Returns an empty list when the
    /// user cancelled the sign in dialog.
    /// </summary>
    public static IReadOnlyList<ManagedAccount> ListManagedAccounts()
    {
        PasswordSafeClient? client = GetClient();
        if (client == null)
            return [];

        IReadOnlyList<ManagedAccount> accounts = client.ListManagedAccounts();

        lock (AccountCache)
        {
            AccountCache.Clear();
            foreach (ManagedAccount account in accounts)
                AccountCache[account.Id] = account;
        }

        return accounts;
    }

    /// <summary>
    /// Releases the password of one account. <paramref name="input"/> is the
    /// UserViaAPI value in "systemId:accountId" form.
    /// </summary>
    public static void FetchSecretFromServer(string input, out string username, out string password, out string domain, out string privatekey)
    {
        username = "";
        password = "";
        domain = "";
        privatekey = "";

        (_, int accountId) = ManagedAccount.ParseId(input);

        PasswordSafeClient? client = GetClient();
        if (client == null)
            return;

        ManagedAccount account = FindAccount(input)
            ?? throw new PasswordSafeException($"Managed account '{input}' was not found or is not requestable.");

        ReleasedCredential credential = client.Release(account, "mRemoteNG connection");

        lock (ActiveRequests)
            ActiveRequests[accountId] = credential.RequestId;

        username = credential.Username;
        password = credential.Password;
        domain = credential.Domain;
    }

    /// <summary>
    /// Checks the release held for this account back in. Safe to call when nothing is
    /// held. <paramref name="input"/> uses the same "systemId:accountId" form.
    /// </summary>
    public static void CheckIn(string input)
    {
        (_, int accountId) = ManagedAccount.ParseId(input);

        int requestId;
        lock (ActiveRequests)
        {
            if (!ActiveRequests.Remove(accountId, out requestId))
                return;
        }

        _client?.CheckIn(requestId);
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

        PasswordSafeClient? client = _client;
        if (client != null)
        {
            foreach (int requestId in requestIds)
            {
                try
                {
                    client.CheckIn(requestId);
                }
                catch (Exception)
                {
                    // best effort during shutdown
                }
            }
        }

        lock (SyncRoot)
        {
            _client?.Dispose();
            _client = null;
        }

        lock (AccountCache)
            AccountCache.Clear();
    }

    /// <summary>
    /// Looks an account up in the result of the last enumeration, enumerating once if
    /// nothing has been fetched yet. The API has no "get requestable account by id".
    /// </summary>
    private static ManagedAccount? FindAccount(string id)
    {
        lock (AccountCache)
        {
            if (AccountCache.TryGetValue(id, out ManagedAccount? cached))
                return cached;
        }

        ListManagedAccounts();

        lock (AccountCache)
            return AccountCache.GetValueOrDefault(id);
    }
}
