using tusdotnet.Stores.S3.Helpers;

namespace tusdotnet.Stores.S3.Tests;

public static class ThreadingTools
{
    /// <summary>
    /// Wraps a task with one that will complete as cancelled based on a cancellation token,
    /// allowing someone to await a task but be able to break out early by cancelling the token.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the task.</typeparam>
    /// <param name="task">The task to wrap.</param>
    /// <param name="cancellationToken">The token that can be canceled to break out of the await.</param>
    /// <returns>The wrapping task.</returns>
    public static Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        Requires.NotNull(task, nameof(task));

        if (!cancellationToken.CanBeCanceled || task.IsCompleted)
        {
            return task;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }

        return WithCancellationSlow(task, cancellationToken);
    }
    
    /// <summary>
    /// Wraps a task with one that will complete as cancelled based on a cancellation token,
    /// allowing someone to await a task but be able to break out early by cancelling the token.
    /// </summary>
    /// <param name="task">The task to wrap.</param>
    /// <param name="cancellationToken">The token that can be canceled to break out of the await.</param>
    /// <returns>The wrapping task.</returns>
    public static Task WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        Requires.NotNull(task, nameof(task));

        if (!cancellationToken.CanBeCanceled || task.IsCompleted)
        {
            return task;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        return WithCancellationSlow(task, continueOnCapturedContext: false, cancellationToken: cancellationToken);
    }
    
    /// <summary>
    /// Wraps a task with one that will complete as cancelled based on a cancellation token,
    /// allowing someone to await a task but be able to break out early by cancelling the token.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the task.</typeparam>
    /// <param name="task">The task to wrap.</param>
    /// <param name="cancellationToken">The token that can be canceled to break out of the await.</param>
    /// <returns>The wrapping task.</returns>
    private static async Task<T> WithCancellationSlow<T>(Task<T> task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();

        await using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), tcs))
        {
            if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        // Rethrow any fault/cancellation exception, even if we awaited above.
        // But if we skipped the above if branch, this will actually yield
        // on an incompleted task.
        return await task.ConfigureAwait(false);
    }
    
    /// <summary>
    /// Wraps a task with one that will complete as cancelled based on a cancellation token,
    /// allowing someone to await a task but be able to break out early by cancelling the token.
    /// </summary>
    /// <param name="task">The task to wrap.</param>
    /// <param name="continueOnCapturedContext">A value indicating whether *internal* continuations required to respond to cancellation should run on the current <see cref="SynchronizationContext"/>.</param>
    /// <param name="cancellationToken">The token that can be canceled to break out of the await.</param>
    /// <returns>The wrapping task.</returns>
    private static async Task WithCancellationSlow(this Task task, bool continueOnCapturedContext, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();

        await using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), tcs))
        {
            if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(continueOnCapturedContext))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        // Rethrow any fault/cancellation exception, even if we awaited above.
        // But if we skipped the above if branch, this will actually yield
        // on an incompleted task.
        await task.ConfigureAwait(continueOnCapturedContext);
    }
}
