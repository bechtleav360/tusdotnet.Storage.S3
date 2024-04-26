using System;
using System.Diagnostics;

namespace tusdotnet.Stores.S3.Helpers;

/// <summary>
/// Common runtime checks that throw ArgumentExceptions upon failure.
/// </summary>
public static class Requires
{
    /// <summary>
    /// Throws an exception if the specified parameter's value is null.
    /// </summary>
    /// <typeparam name="T">The type of the parameter.</typeparam>
    /// <param name="value">The value of the argument.</param>
    /// <param name="parameterName">The name of the parameter to include in any thrown exception. If this argument is omitted (explicitly writing <see langword="null" /> does not qualify), the expression used in the first argument will be used as the parameter name.</param>
    /// <returns>The value of the parameter.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is <see langword="null"/>.</exception>
    [DebuggerStepThrough]
    public static T NotNull<T>(T value, string? parameterName = null)
        where T : class // ensures value-types aren't passed to a null checking method
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        return value;
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> if a condition does not evaluate to true.
    /// </summary>
    [DebuggerStepThrough]
    public static void Range(bool condition, string? parameterName, string? message = null)
    {
        if (!condition)
        {
            FailRange(parameterName, message);
        }
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> if a condition does not evaluate to true.
    /// </summary>
    /// <returns>Nothing.  This method always throws.</returns>
    [DebuggerStepThrough]
    public static Exception FailRange(string? parameterName, string? message = null)
    {
        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        throw new ArgumentOutOfRangeException(parameterName, message);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> if a condition does not evaluate to true.
    /// </summary>
    [DebuggerStepThrough]
    public static void Argument(bool condition, string? parameterName, string? message)
    {
        if (!condition)
        {
            throw new ArgumentException(message, parameterName);
        }
    }
}