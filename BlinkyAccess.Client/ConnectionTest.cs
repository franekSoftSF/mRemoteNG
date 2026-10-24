namespace BlinkyAccess.Client;

/// <summary>
/// Outcome of <see cref="BlinkyAccessClient.TestConnection"/>. Never contains the
/// token, a header or any part of the response body.
/// </summary>
public sealed class BlinkyAccessTestResult
{
    public required bool Success { get; init; }

    /// <summary>One line the options page can show as is.</summary>
    public required string Detail { get; init; }

    /// <summary>How many connections the portal returned, when the call succeeded.</summary>
    public int ConnectionCount { get; init; }

    public override string ToString() => $"{(Success ? "OK" : "FAIL")}  {Detail}";
}
