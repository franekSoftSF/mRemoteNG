namespace BeyondTrust.PasswordSafe.Client;

/// <summary>
/// A requestable managed account, as returned by GET ManagedAccounts and enriched
/// with the connection details of its managed system and the name of its platform.
/// </summary>
public sealed class ManagedAccount
{
    // fields of ManagedAccountRequestableModel
    public int AccountId { get; set; }
    public string AccountName { get; set; } = "";
    public string? AccountDescription { get; set; }
    public int SystemId { get; set; }
    public string SystemName { get; set; } = "";
    public int PlatformID { get; set; }
    public string? DomainName { get; set; }
    public string? UserPrincipalName { get; set; }
    public string? InstanceName { get; set; }
    public bool IsISAAccess { get; set; }
    public int MaximumReleaseDuration { get; set; }
    public int? DefaultReleaseDuration { get; set; }

    // resolved from the managed system and the platform of this account
    public string Host { get; set; } = "";
    public int? Port { get; set; }
    public string PlatformName { get; set; } = "";

    /// <summary>
    /// Stable identifier for one account, in "systemId:accountId" form. Both ids are
    /// needed because a release request takes the system and the account.
    /// </summary>
    public string Id => $"{SystemId}:{AccountId}";

    /// <summary>
    /// How long a release may last: the configured default, otherwise an hour capped
    /// by the maximum the policy allows.
    /// </summary>
    public int ReleaseDurationMinutes
    {
        get
        {
            int duration = DefaultReleaseDuration ?? 0;
            if (duration > 0)
                return duration;

            return MaximumReleaseDuration > 0 ? Math.Min(60, MaximumReleaseDuration) : 60;
        }
    }

    /// <summary>Splits an <see cref="Id"/> back into its two parts.</summary>
    public static (int SystemId, int AccountId) ParseId(string id)
    {
        string[] parts = (id ?? "").Split(':', StringSplitOptions.TrimEntries);

        if (parts.Length == 2 && int.TryParse(parts[0], out int systemId) && int.TryParse(parts[1], out int accountId))
            return (systemId, accountId);

        throw new FormatException($"'{id}' is not a valid managed account identifier. Expected 'systemId:accountId'.");
    }
}

/// <summary>A released credential. The password is only ever held by the caller.</summary>
public sealed class ReleasedCredential
{
    public required int RequestId { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public string Domain { get; init; } = "";
}
