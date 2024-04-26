using System.Buffers;
using tusdotnet.Stores.S3.Extensions;
using Xunit.Abstractions;

namespace tusdotnet.Stores.S3.Tests;

/// <summary>
/// Exposes <see cref="ReadOnlySequence{T}"/> as a <see cref="Stream"/>
/// </summary>
/// <remarks>
/// Copy of https://github.com/dotnet/Nerdbank.Streams/blob/main/src/Nerdbank.Streams/ReadOnlySequenceStream.cs
/// </remarks>
public class ReadOnlySequenceStreamTests : TestBase
{
    private static readonly ReadOnlySequence<byte> _defaultSequence = default;

    private static readonly ReadOnlySequence<byte> _simpleSequence = new ReadOnlySequence<byte>(new byte[] { 1, 2, 3 });

    private static readonly ReadOnlySequence<byte> _multiBlockSequence;

    private readonly Stream _defaultStream;

    static ReadOnlySequenceStreamTests()
    {
        var seg3 = new SeqSegment(new byte[] { 7, 8, 9 }, null);
        var seg2 = new SeqSegment(new byte[] { 4, 5, 6 }, seg3);
        var seg1 = new SeqSegment(new byte[] { 1, 2, 3 }, seg2);
        _multiBlockSequence = new ReadOnlySequence<byte>(seg1, 0, seg3, seg3.Memory.Length);
    }

    public ReadOnlySequenceStreamTests(ITestOutputHelper logger)
        : base(logger)
    {
        _defaultStream = _defaultSequence.AsStream();
    }

    [Fact]
    public void Read_EmptySequence()
    {
        Assert.Equal(0, _defaultStream.Read(new byte[1], 0, 1));
    }

    [Fact]
    public void Length()
    {
        Assert.Equal(0, _defaultStream.Length);
        _defaultStream.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _defaultStream.Length);
    }

    [Fact]
    public void SetLength()
    {
        Assert.Throws<NotSupportedException>(() => _defaultStream.SetLength(0));
        _defaultStream.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _defaultStream.SetLength(0));
    }

    [Fact]
    public void CanSeek()
    {
        Assert.True(_defaultStream.CanSeek);
        _defaultStream.Dispose();
        Assert.False(_defaultStream.CanSeek);
    }

    [Fact]
    public void CanRead()
    {
        Assert.True(_defaultStream.CanRead);
        _defaultStream.Dispose();
        Assert.False(_defaultStream.CanRead);
    }

    [Fact]
    public void CanWrite()
    {
        Assert.False(_defaultStream.CanWrite);
        _defaultStream.Dispose();
        Assert.False(_defaultStream.CanWrite);
    }

    [Fact]
    public void CanTimeout()
    {
        Assert.False(_defaultStream.CanTimeout);
        _defaultStream.Dispose();
        Assert.False(_defaultStream.CanTimeout);
    }

    [Fact]
    public void Position()
    {
        Assert.Equal(0, _defaultStream.Position);
        Assert.Throws<ArgumentOutOfRangeException>(() => _defaultStream.Position = 1);

        Stream simpleStream = _simpleSequence.AsStream();
        Assert.Equal(0, simpleStream.Position);
        simpleStream.Position++;
        Assert.Equal(1, simpleStream.Position);

        Stream multiBlockStream = _multiBlockSequence.AsStream();
        Assert.Equal(0, multiBlockStream.Position = 0);
        Assert.Equal(multiBlockStream.Position + 1, multiBlockStream.ReadByte());

        Assert.Equal(4, multiBlockStream.Position = 4);
        Assert.Equal(multiBlockStream.Position + 1, multiBlockStream.ReadByte());

        Assert.Equal(5, multiBlockStream.Position = 5);
        Assert.Equal(multiBlockStream.Position + 1, multiBlockStream.ReadByte());

        Assert.Equal(0, multiBlockStream.Position = 0);
        Assert.Equal(multiBlockStream.Position + 1, multiBlockStream.ReadByte());

        Assert.Equal(9, multiBlockStream.Position = 9);
        Assert.Equal(-1, multiBlockStream.ReadByte());

        Assert.Throws<ArgumentOutOfRangeException>(() => multiBlockStream.Position = 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => multiBlockStream.Position = -1);
    }

    [Fact]
    public void Flush()
    {
        Assert.Throws<NotSupportedException>(() => _defaultStream.Flush());
        _defaultStream.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _defaultStream.Flush());
    }

    [Fact]
    public async Task FlushAsync()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() => _defaultStream.FlushAsync());
        await _defaultStream.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _defaultStream.FlushAsync());
    }

    [Fact]
    public void Write()
    {
        Assert.Throws<NotSupportedException>(() => _defaultStream.Write(new byte[1], 0, 1));
        _defaultStream.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _defaultStream.Write(new byte[1], 0, 1));
    }

    [Fact]
    public async Task WriteAsync()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() => _defaultStream.WriteAsync(new byte[1], 0, 1));
        await _defaultStream.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _defaultStream.WriteAsync(new byte[1], 0, 1));
    }

    [Fact]
    public void WriteByte()
    {
        Assert.Throws<NotSupportedException>(() => _defaultStream.WriteByte(1));
        _defaultStream.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _defaultStream.WriteByte(1));
    }

    [Fact]
    public void Seek_EmptyStream()
    {
        Stream stream = _defaultSequence.AsStream();
        Assert.Equal(0, stream.Seek(0, SeekOrigin.Begin));
        Assert.Equal(0, stream.Seek(0, SeekOrigin.Current));
        Assert.Equal(0, stream.Seek(0, SeekOrigin.End));
    }

    [Fact]
    public void Seek()
    {
        Stream stream = _multiBlockSequence.AsStream();
        Assert.Equal(0, stream.Seek(0, SeekOrigin.Begin));
        Assert.Equal(0, stream.Position);
        Assert.Equal(stream.Position + 1, stream.ReadByte());

        Assert.Equal(4, stream.Seek(4, SeekOrigin.Begin));
        Assert.Equal(4, stream.Position);
        Assert.Equal(stream.Position + 1, stream.ReadByte());

        Assert.Equal(7, stream.Seek(7, SeekOrigin.Begin));
        Assert.Equal(7, stream.Position);
        Assert.Equal(stream.Position + 1, stream.ReadByte());

        Assert.Equal(9, stream.Seek(1, SeekOrigin.Current));
        Assert.Equal(9, stream.Position);

        Assert.Equal(1, stream.Seek(-8, SeekOrigin.Current));
        Assert.Equal(1, stream.Position);
        Assert.Equal(stream.Position + 1, stream.ReadByte());

        Assert.Equal(5, stream.Seek(3, SeekOrigin.Current));
        Assert.Equal(5, stream.Position);
        Assert.Equal(stream.Position + 1, stream.ReadByte());

        stream.Position = 0;
        Assert.Equal(9, stream.Seek(0, SeekOrigin.End));
        Assert.Equal(9, stream.Position);
        Assert.Equal(-1, stream.ReadByte());

        stream.Position = 0;
        Assert.Equal(8, stream.Seek(-1, SeekOrigin.End));
        Assert.Equal(8, stream.Position);
        Assert.Equal(stream.Position + 1, stream.ReadByte());

        Assert.Equal(5, stream.Seek(-4, SeekOrigin.End));
        Assert.Equal(5, stream.Position);
        Assert.Equal(stream.Position + 1, stream.ReadByte());

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Seek(1, SeekOrigin.End));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Seek(-1, SeekOrigin.Begin));

        stream.Dispose();
        Assert.Throws<ObjectDisposedException>(() => stream.Seek(0, SeekOrigin.Begin));
    }

    [Fact]
    public void ReadByte()
    {
        Stream stream = _multiBlockSequence.AsStream();

        for (int i = 0; i < _multiBlockSequence.Length; i++)
        {
            Assert.Equal(i + 1, stream.ReadByte());
        }

        Assert.Equal(-1, stream.ReadByte());
        Assert.Equal(-1, stream.ReadByte());
    }

    [Fact]
    public void Read()
    {
        Stream stream = _multiBlockSequence.AsStream();
        byte[] buffer = new byte[_multiBlockSequence.Length + 2];
        Assert.Equal(2, stream.Read(buffer, 0, 2));
        Assert.Equal(new byte[] { 1, 2, 0 }, buffer.Take(3));
        Assert.Equal(2, stream.Position);

        Assert.Equal(2, stream.Read(buffer, 3, 2));
        Assert.Equal(new byte[] { 1, 2, 0, 3, 4, 0 }, buffer.Take(6));

        Assert.Equal(5, stream.Read(buffer, 5, buffer.Length - 5));
        Assert.Equal(new byte[] { 1, 2, 0, 3, 4, 5, 6, 7, 8, 9, 0 }, buffer);
        Assert.Equal(9, stream.Position);

        Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(9, stream.Position);
    }

    [Fact]
    public void ReadAsync_ReturnsSynchronously()
    {
        Stream stream = _simpleSequence.AsStream();
        Assert.True(stream.ReadAsync(new byte[1], 0, 1).IsCompleted);
    }

    [Fact]
    public async Task ReadAsync_ReusesTaskResult()
    {
        Stream stream = _multiBlockSequence.AsStream();
        Task<int> task1 = stream.ReadAsync(new byte[1], 0, 1);
        Task<int> task2 = stream.ReadAsync(new byte[1], 0, 1);
        Assert.Same(task1, task2);
        Assert.Equal(1, await task1);

        Task<int> task3 = stream.ReadAsync(new byte[2], 0, 2);
        Task<int> task4 = stream.ReadAsync(new byte[2], 0, 2);
        Assert.Same(task3, task4);
        Assert.Equal(2, await task3);
    }

    [Fact]
    public async Task ReadAsync_Works()
    {
        Stream stream = _multiBlockSequence.AsStream();
        byte[] buffer = new byte[_multiBlockSequence.Length + 2];
        Assert.Equal(2, await stream.ReadAsync(buffer, 0, 2));
        Assert.Equal(new byte[] { 1, 2, 0 }, buffer.Take(3));
        Assert.Equal(2, stream.Position);

        Assert.Equal(2, await stream.ReadAsync(buffer, 3, 2));
        Assert.Equal(new byte[] { 1, 2, 0, 3, 4, 0 }, buffer.Take(6));

        Assert.Equal(5, await stream.ReadAsync(buffer, 5, buffer.Length - 5));
        Assert.Equal(new byte[] { 1, 2, 0, 3, 4, 5, 6, 7, 8, 9, 0 }, buffer);
        Assert.Equal(9, stream.Position);

        Assert.Equal(0, await stream.ReadAsync(buffer, 0, buffer.Length));
        Assert.Equal(0, await stream.ReadAsync(buffer, 0, buffer.Length));
        Assert.Equal(9, stream.Position);
    }

    [Fact]
    public async Task CopyToAsync()
    {
        Stream stream = _multiBlockSequence.AsStream();
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        Assert.Equal(_multiBlockSequence.ToArray(), ms.ToArray());
    }
    
    [Fact]
    public void IsDisposed()
    {
        Assert.False(((IDisposableObservable)_defaultStream).IsDisposed);
        _defaultStream.Dispose();
        Assert.True(((IDisposableObservable)_defaultStream).IsDisposed);
    }

    private class SeqSegment : ReadOnlySequenceSegment<byte>
    {
        public SeqSegment(byte[] buffer, SeqSegment? next)
        {
            Memory = buffer;
            Next = next;

            SeqSegment current = this;
            while (next != null)
            {
                next.RunningIndex = current.RunningIndex + current.Memory.Length;
                current = next;
                next = (SeqSegment?)next.Next;
            }
        }
    }
}