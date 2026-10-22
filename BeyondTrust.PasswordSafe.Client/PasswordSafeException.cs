using System.Net;

namespace BeyondTrust.PasswordSafe.Client;

/// <summary>A call to Password Safe failed.</summary>
public class PasswordSafeException : Exception
{
    public PasswordSafeException(string message) : base(message)
    {
    }

    public PasswordSafeException(string message, HttpStatusCode statusCode, string? responseBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// The response body, when it was safe to keep. Calls that return a credential
    /// never fill this in, so a secret cannot travel inside an exception.
    /// </summary>
    public string? ResponseBody { get; }
}

/// <summary>
/// Password Safe asked for a second factor. The prompt it sent is in
/// <see cref="Prompt"/>; sign in again with <see cref="PasswordSafeOptions.Challenge"/>
/// filled in.
/// </summary>
public sealed class PasswordSafeChallengeRequiredException(string prompt)
    : PasswordSafeException($"Two factor authentication required: {prompt}")
{
    public string Prompt { get; } = prompt;
}

/// <summary>
/// The account cannot be released: either the user may not request it, the request
/// still needs approval, or the account is not enabled for API access.
/// </summary>
public sealed class PasswordSafeAccessDeniedException(string message, HttpStatusCode statusCode, string? responseBody)
    : PasswordSafeException(message, statusCode, responseBody);
