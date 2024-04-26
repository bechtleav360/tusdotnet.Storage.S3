using System.Buffers;
using System.IO;

namespace tusdotnet.Stores.S3.Extensions;

/// <summary>
/// Stream extension methods.
/// </summary>
public static class StreamExtensions
{
    /// <summary>
    /// Creates a <see cref="Stream"/> that can read no more than a given number of bytes from an underlying stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="length">The number of bytes to read from the parent stream.</param>
    /// <returns>A stream that ends after <paramref name="length"/> bytes are read.</returns>
    public static Stream ReadSlice(this Stream stream, long length) => new NestedStream(stream, length);

    /// <summary>
    /// Exposes a <see cref="ReadOnlySequence{T}"/> of <see cref="byte"/> as a <see cref="Stream"/>.
    /// </summary>
    /// <param name="readOnlySequence">The sequence of bytes to expose as a stream.</param>
    /// <returns>The readable stream.</returns>
    public static Stream AsStream(this ReadOnlySequence<byte> readOnlySequence) =>
        new ReadOnlySequenceStream(readOnlySequence, null, null);
}