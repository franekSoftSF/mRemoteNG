namespace BeyondTrust.PasswordSafe.Client;

/// <summary>One checked step of <see cref="PasswordSafeClient.TestConnection"/>.</summary>
public sealed record ConnectionTestStep(string Name, bool Ok, string Detail)
{
    public override string ToString() => $"{(Ok ? "OK  " : "FAIL")}  {Name}: {Detail}";
}

/// <summary>Outcome of a connection test. Never contains a credential.</summary>
public sealed class ConnectionTestResult
{
    public List<ConnectionTestStep> Steps { get; } = [];

    public bool Success => Steps.Count > 0 && Steps.TrueForAll(step => step.Ok);

    public override string ToString() => string.Join(Environment.NewLine, Steps);
}
