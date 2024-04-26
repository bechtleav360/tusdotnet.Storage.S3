using System;

namespace tusdotnet.Stores.S3.Extensions;

public static class DateTimeOffsetExtensions
{
    public static bool HasPassed(this DateTimeOffset dateTime)
    {
        return dateTime.ToUniversalTime().CompareTo(DateTimeOffset.UtcNow) == -1;
    }
}
