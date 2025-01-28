using System;

namespace tusdotnet.Stores.S3.Extensions;

/// <summary>
/// Extension methods for <see cref="DateTimeOffset"/>.
/// </summary>
public static class DateTimeOffsetExtensions
{
    /// <summary>
    /// Check if the given <paramref name="dateTime"/> has passed.
    /// </summary>
    /// <param name="dateTime">the <see cref="DateTimeOffset"/> object to act upon</param>
    /// <returns></returns>
    public static bool HasPassed(this DateTimeOffset dateTime)
    {
        return dateTime.ToUniversalTime().CompareTo(DateTimeOffset.UtcNow) == -1;
    }
}
