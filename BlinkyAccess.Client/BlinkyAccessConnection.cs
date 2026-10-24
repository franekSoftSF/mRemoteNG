namespace BlinkyAccess.Client;

/// <summary>
/// One connection the signed in portal user may open, as returned by
/// GET /api/user/v1/connections.
/// </summary>
public sealed class BlinkyAccessConnection
{
    /// <summary>
    /// Stable identifier of this connection, the same for as long as the host and the
    /// login stay the same. Used as the identity of the mirrored connection.
    /// </summary>
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>Folder the portal groups this connection under; may be empty.</summary>
    public string Folder { get; set; } = "";

    public string Hostname { get; set; } = "";

    /// <summary>Port to connect to, or null to use the protocol default.</summary>
    public int? Port { get; set; }

    /// <summary>Protocol name as the portal spells it, for example <c>SSH2</c>.</summary>
    public string Protocol { get; set; } = "";

    public string Username { get; set; } = "";

    /// <summary>
    /// Smart card or FIDO certificate specifier for PuTTY-CAC, for example
    /// <c>FIDO:ssh:blinky</c>. Empty when the connection does not use one.
    /// </summary>
    public string SmartcardCertificate { get; set; } = "";

    public bool SshAgentForwarding { get; set; }

    public string Description { get; set; } = "";
}

/// <summary>
/// The response envelope of the connections endpoint. <see cref="Format"/> is checked
/// before anything is read out of <see cref="Connections"/>.
/// </summary>
public sealed class BlinkyAccessConnectionsResponse
{
    public string Format { get; set; } = "";

    public List<BlinkyAccessConnection> Connections { get; set; } = [];
}
