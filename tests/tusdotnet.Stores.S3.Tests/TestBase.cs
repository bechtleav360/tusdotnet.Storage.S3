using System.Diagnostics;
using Xunit.Abstractions;

namespace tusdotnet.Stores.S3.Tests;

public class TestBase
{
    private static TimeSpan TestTimeout => UnexpectedTimeout;
    private readonly CancellationTokenSource _timeoutTokenSource;

    protected static readonly TimeSpan UnexpectedTimeout =
        Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(10);

    protected ITestOutputHelper Logger { get; }

    protected CancellationToken TimeoutToken =>
        Debugger.IsAttached ? CancellationToken.None : _timeoutTokenSource.Token;

    protected TestBase(ITestOutputHelper logger)
    {
        Logger = logger;
        _timeoutTokenSource = new CancellationTokenSource(TestTimeout);
    }
}
