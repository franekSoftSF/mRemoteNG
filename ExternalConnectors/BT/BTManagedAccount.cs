namespace ExternalConnectors.BT;

/// <summary>
/// A requestable managed account as returned by GET ManagedAccounts, enriched with
/// the connection details of its managed system (GET ManagedSystems) and the name
/// of its platform (GET Platforms).
/// </summary>
public class BTManagedAccount
{
    // fields returned by GET ManagedAccounts (ManagedAccountRequestableModel)
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

    // looked up from the managed system / platform of this account
    public string Host { get; set; } = "";
    public int? Port { get; set; }
    public string PlatformName { get; set; } = "";

    /// <summary>
    /// Identifier handed to mRemoteNG as UserViaAPI. Both ids are needed because
    /// POST Requests takes SystemID and AccountID.
    /// </summary>
    public string SecretId => $"{SystemId}:{AccountId}";
}
