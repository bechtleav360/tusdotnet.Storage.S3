using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Stores.S3.Helpers;

namespace tusdotnet.Stores.S3;

internal class ReadOnlySequenceStream : Stream, IDisposableObservable
{
    private static readonly Task<int> _taskOfZero = Task.FromResult(0);

    private readonly Action<object?>? _disposeAction;
    private readonly object? _disposeActionArg;

    private readonly ReadOnlySequence<byte> _readOnlySequence;

    /// <summary>
    /// A reusable task if two consecutive reads return the same number of bytes.
    /// </summary>
    private Task<int>? _lastReadTask;

    private SequencePosition _position;

    /// <inheritdoc/>
    public override bool CanRead => !IsDisposed;

    /// <inheritdoc/>
    public override bool CanSeek => !IsDisposed;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => ReturnOrThrowDisposed(_readOnlySequence.Length);

    /// <inheritdoc/>
    public override long Position
    {
        get => _readOnlySequence.Slice(0, _position).Length;
        set
        {
            Requires.Range(value >= 0, nameof(value));
            _position = _readOnlySequence.GetPosition(value, _readOnlySequence.Start);
        }
    }

    public bool IsDisposed { get; private set; }

    internal ReadOnlySequenceStream(ReadOnlySequence<byte> readOnlySequence, Action<object?>? disposeAction, object? disposeActionArg)
    {
        _readOnlySequence = readOnlySequence;
        _disposeAction = disposeAction;
        _disposeActionArg = disposeActionArg;
        _position = readOnlySequence.Start;
    }

    private T ReturnOrThrowDisposed<T>(T value)
    {
        NotDisposed();
        
        return value;
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

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            _disposeAction?.Invoke(_disposeActionArg);
            base.Dispose(disposing);
        }
    }

    /// <inheritdoc/>
    public override void Flush() => ThrowDisposedOr(new NotSupportedException());

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken) => throw ThrowDisposedOr(new NotSupportedException());

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ReadOnlySequence<byte> remaining = _readOnlySequence.Slice(_position);
        ReadOnlySequence<byte> toCopy = remaining.Slice(0, Math.Min(count, remaining.Length));
        _position = toCopy.End;
        toCopy.CopyTo(buffer.AsSpan(offset, count));
        return (int)toCopy.Length;
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int bytesRead = Read(buffer, offset, count);
        if (bytesRead == 0)
        {
            return _taskOfZero;
        }

        if (_lastReadTask?.Result == bytesRead)
        {
            return _lastReadTask;
        }
        else
        {
            return _lastReadTask = Task.FromResult(bytesRead);
        }
    }

    /// <inheritdoc/>
    public override int ReadByte()
    {
        ReadOnlySequence<byte> remaining = _readOnlySequence.Slice(_position);
        if (remaining.Length > 0)
        {
            byte result = remaining.First.Span[0];
            _position = _readOnlySequence.GetPosition(1, _position);
            return result;
        }
        else
        {
            return -1;
        }
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        NotDisposed();

        SequencePosition relativeTo;
        switch (origin)
        {
            case SeekOrigin.Begin:
                relativeTo = _readOnlySequence.Start;
                break;
            case SeekOrigin.Current:
                if (offset >= 0)
                {
                    relativeTo = _position;
                }
                else
                {
                    relativeTo = _readOnlySequence.Start;
                    offset += Position;
                }

                break;
            case SeekOrigin.End:
                if (offset >= 0)
                {
                    relativeTo = _readOnlySequence.End;
                }
                else
                {
                    relativeTo = _readOnlySequence.Start;
                    offset += Length;
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin));
        }

        _position = _readOnlySequence.GetPosition(offset, relativeTo);
        return Position;
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => ThrowDisposedOr(new NotSupportedException());

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => ThrowDisposedOr(new NotSupportedException());

    /// <inheritdoc/>
    public override void WriteByte(byte value) => ThrowDisposedOr(new NotSupportedException());

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw ThrowDisposedOr(new NotSupportedException());

    /// <inheritdoc/>
    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        foreach (ReadOnlyMemory<byte> segment in _readOnlySequence)
        {
            await destination.WriteAsync(segment, cancellationToken).ConfigureAwait(false);
        }
    }
}