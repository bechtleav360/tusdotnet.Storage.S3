using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Stores.S3.Helpers;

namespace tusdotnet.Stores.S3;

/// <summary>
/// A stream that allows for reading from another stream up to a given number of bytes.
/// </summary>
internal class NestedStream : Stream, IDisposableObservable
{
    /// <summary>
    /// The total length of the stream.
    /// </summary>
    private readonly long _length;

    /// <summary>
    /// The stream to read from.
    /// </summary>
    private readonly Stream _underlyingStream;

    /// <summary>
    /// The remaining bytes allowed to be read.
    /// </summary>
    private long _remainingBytes;

    /// <inheritdoc />
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => !IsDisposed;

    /// <inheritdoc />
    public override bool CanSeek => !IsDisposed && _underlyingStream.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length
    {
        get
        {
            NotDisposed();

            return _underlyingStream.CanSeek ? _length : throw new NotSupportedException();
        }
    }

    /// <inheritdoc />
    public override long Position
    {
        get
        {
            NotDisposed();

            return _length - _remainingBytes;
        }

        set { Seek(value, SeekOrigin.Begin); }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NestedStream"/> class.
    /// </summary>
    /// <param name="underlyingStream">The stream to read from.</param>
    /// <param name="length">The number of bytes to read from the parent stream.</param>
    public NestedStream(Stream underlyingStream, long length)
    {
        Requires.NotNull(underlyingStream, nameof(underlyingStream));
        Requires.Range(length >= 0, nameof(length));
        Requires.Argument(underlyingStream.CanRead, nameof(underlyingStream), "Stream must be readable.");

        _underlyingStream = underlyingStream;
        _remainingBytes = length;
        _length = length;
    }

    private Exception ThrowDisposedOr(Exception ex)
    {
        NotDisposed();

        throw ex;
    }

    private void NotDisposed()
    {
        if (IsDisposed)
        {
            string objectName = GetType().FullName ?? string.Empty;

            throw new ObjectDisposedException(objectName);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override void Flush() => ThrowDisposedOr(new NotSupportedException());

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) =>
        throw ThrowDisposedOr(new NotSupportedException());

    /// <inheritdoc />
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        NotDisposed();

        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || count < 0)
        {
            throw new ArgumentOutOfRangeException();
        }

        if (offset + count > buffer.Length)
        {
            throw new ArgumentException();
        }

        count = (int)Math.Min(count, _remainingBytes);

        if (count <= 0)
        {
            return 0;
        }

        int bytesRead = await _underlyingStream.ReadAsync(buffer, offset, count).ConfigureAwait(true);
        _remainingBytes -= bytesRead;

        return bytesRead;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        NotDisposed();

        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || count < 0)
        {
            throw new ArgumentOutOfRangeException();
        }

        if (offset + count > buffer.Length)
        {
            throw new ArgumentException();
        }

        count = (int)Math.Min(count, _remainingBytes);

        if (count <= 0)
        {
            return 0;
        }

        int bytesRead = _underlyingStream.Read(buffer, offset, count);
        _remainingBytes -= bytesRead;

        return bytesRead;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        NotDisposed();

        if (!CanSeek)
        {
            throw new NotSupportedException("The underlying stream does not support seeking.");
        }

        // Recalculate offset relative to the current position
        long newOffset = origin switch
        {
            SeekOrigin.Current => offset,
            SeekOrigin.End => _length + offset - Position,
            SeekOrigin.Begin => offset - Position,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), "Invalid seek origin."),
        };

        // Determine whether the requested position is within the bounds of the stream
        if (Position + newOffset < 0)
        {
            throw new IOException("An attempt was made to move the position before the beginning of the stream.");
        }

        long currentPosition = _underlyingStream.Position;
        long newPosition = _underlyingStream.Seek(newOffset, SeekOrigin.Current);
        _remainingBytes -= newPosition - currentPosition;

        return Position;
    }

    /// <inheritdoc />
    public override void SetLength(long value) => throw ThrowDisposedOr(new NotSupportedException());

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        NotDisposed();

        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        NotDisposed();

        throw new NotSupportedException();
    }
}
