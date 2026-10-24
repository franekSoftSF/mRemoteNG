namespace BlinkyAccess.Client;

/// <summary>
/// Everything needed to reach a Blinky Access portal. The token lives here only for
/// as long as the client does; nothing in this library writes it anywhere.
/// </summary>
public sealed class BlinkyAccessOptions
{
    /// <summary>Portal address, for example https://portal.example.com.</summary>
    public string PortalUrl { get; set; } = "";

    /// <summary>
    /// Personal access token of the portal user. Starts with <c>bla_pat_</c> and is
    /// only ever sent in the Authorization header, never in a URL.
    /// </summary>
    public string Token { get; set; } = "";

    /// <summary>
    /// How long one call may take. Enforced per request, so the shared
    /// <see cref="System.Net.Http.HttpClient"/> can keep its own longer timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Sent as User-Agent, so the calls are recognisable in the portal log.</summary>
    public string UserAgent { get; set; } = "mRemoteNG";

    /// <summary>The prefix every Blinky Access personal access token carries.</summary>
    public const string TokenPrefix = "bla_pat_";

    /// <summary>Path of the connections endpoint below the portal address.</summary>
    public const string ConnectionsPath = "/api/user/v1/connections";

    /// <summary>
    /// The connections endpoint of <see cref="PortalUrl"/>. A missing scheme is read as
    /// https; anything other than https is refused, because the token travels in a
    /// header and must not cross a plain text connection.
    ///
    /// The endpoint is what the documentation shows, so it is what people paste: an
    /// address that already ends in it is taken as it is rather than having a second
    /// copy of the path appended.
    /// </summary>
    public Uri ConnectionsUrl
    {
        get
        {
            string url = NormalizedPortalUrl();

            return new Uri(url.EndsWith(ConnectionsPath, StringComparison.OrdinalIgnoreCase)
                ? url
                : url + ConnectionsPath);
        }
    }

    /// <summary>
    /// Whether an address can be used at all, without building a request. Reported by
    /// the options page while the user is still typing.
    /// </summary>
    public static bool IsUsablePortalUrl(string portalUrl)
    {
        try
        {
            _ = new BlinkyAccessOptions { PortalUrl = portalUrl }.ConnectionsUrl;
            return true;
        }
        catch (BlinkyAccessException)
        {
            return false;
        }
    }

    private string NormalizedPortalUrl()
    {
        string url = (PortalUrl ?? "").Trim();

        if (url.Length == 0)
            throw new BlinkyAccessConfigurationException("No portal address is configured.");

        if (!url.Contains("://", StringComparison.Ordinal))
            url = "https://" + url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed))
            throw new BlinkyAccessConfigurationException($"'{PortalUrl}' is not a valid portal address.");

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new BlinkyAccessConfigurationException("The portal address must start with https://.");

        return url.TrimEnd('/');
    }
}
