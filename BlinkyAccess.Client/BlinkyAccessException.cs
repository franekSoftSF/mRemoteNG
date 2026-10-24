using System.Net;

namespace BlinkyAccess.Client;

/// <summary>A call to a Blinky Access portal failed.</summary>
public class BlinkyAccessException : Exception
{
    public BlinkyAccessException(string message) : base(message)
    {
    }

    public BlinkyAccessException(string message, Exception? innerException) : base(message, innerException)
    {
    }

    public BlinkyAccessException(string message, HttpStatusCode statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}

/// <summary>
/// The portal address or the token is missing or cannot be used. Raised before any
/// request is sent, so nothing was contacted.
/// </summary>
public sealed class BlinkyAccessConfigurationException(string message) : BlinkyAccessException(message);

/// <summary>
/// The portal refused the token: it is wrong, expired or has been revoked. A new token
/// has to be issued; repeating the same call cannot help.
/// </summary>
public sealed class BlinkyAccessUnauthorizedException()
    : BlinkyAccessException("The portal rejected the access token.", HttpStatusCode.Unauthorized);

/// <summary>
/// The portal is rate limiting this user. The call may be repeated later, after
/// <see cref="RetryAfter"/> when the portal said how long.
/// </summary>
public sealed class BlinkyAccessThrottledException(TimeSpan? retryAfter)
    : BlinkyAccessException("The portal is rate limiting this client.", HttpStatusCode.TooManyRequests)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

/// <summary>
/// The portal answered in a format this version does not understand. The connections
/// are not read, so nothing in the tree is touched.
/// </summary>
public sealed class BlinkyAccessUnsupportedFormatException(string? reportedFormat)
    : BlinkyAccessException($"Unsupported response format '{reportedFormat}'; expected '{BlinkyAccessClient.SupportedFormat}'.")
{
    public string? ReportedFormat { get; } = reportedFormat;
}
