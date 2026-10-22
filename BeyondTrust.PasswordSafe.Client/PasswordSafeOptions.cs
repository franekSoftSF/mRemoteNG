namespace BeyondTrust.PasswordSafe.Client;

/// <summary>
/// Everything needed to reach a Password Safe instance. The secrets live here only
/// for as long as the client does; nothing in this library writes them anywhere.
/// </summary>
public sealed class PasswordSafeOptions
{
    /// <summary>
    /// Base address, either the installation root (https://ps.example.com/BeyondTrust)
    /// or the full API path. The v3 path is appended when it is missing.
    /// </summary>
    public string Url { get; set; } = "";

    /// <summary>API key of the application registered in BeyondInsight.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>BeyondInsight user the API key runs as.</summary>
    public string RunAsUser { get; set; } = "";

    /// <summary>Only needed when the API registration requires the user's password.</summary>
    public string RunAsPassword { get; set; } = "";

    /// <summary>Second factor, when the registration asks for one.</summary>
    public string Challenge { get; set; } = "";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>Sent as User-Agent, so the calls are recognisable in the audit log.</summary>
    public string UserAgent { get; set; } = "mRemoteNG";

    /// <summary>The API base address with the v3 path, without a trailing slash.</summary>
    public string ApiBase
    {
        get
        {
            string url = Url.TrimEnd('/');
            return url.Contains("/api/public/", StringComparison.OrdinalIgnoreCase)
                ? url
                : url + "/api/public/v3";
        }
    }
}
