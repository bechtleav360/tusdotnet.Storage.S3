using tusdotnet.Stores.S3.Helpers;

namespace tusdotnet.Stores.S3.Tests;

internal class OneWayStreamWrapper : Stream
{
    private readonly Stream _innerStream;
    private readonly bool _canRead;
    private readonly bool _canWrite;

    internal OneWayStreamWrapper(Stream innerStream, bool canRead = false, bool canWrite = false)
    {
        if (canRead == canWrite)
        {
            throw new ArgumentException("Exactly one operation (read or write) must be true.");
        }

        Requires.Argument(innerStream.CanRead || !canRead, nameof(canRead), "Underlying stream is not readable.");
        Requires.Argument(innerStream.CanWrite || !canWrite, nameof(canWrite), "Underlying stream is not writeable.");

        _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        _canRead = canRead;
        _canWrite = canWrite;
    }

    public override bool CanRead => _canRead && _innerStream.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => _canWrite && _innerStream.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        if (CanWrite)
        {
            _innerStream.Flush();
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (CanRead)
        {
            return _innerStream.Read(buffer, offset, count);
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (CanRead)
        {
            return _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (CanWrite)
        {
            _innerStream.Write(buffer, offset, count);
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (CanWrite)
        {
            return _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _innerStream.Dispose();
        }
    }
}
