namespace tusdotnet.Stores.S3;

/// <summary>
/// Represents an object that can be disposed and observed if it has been disposed.
/// </summary>
public interface IDisposableObservable
{
    /// <summary>
    /// Check if the object has been disposed.
    /// </summary>
    bool IsDisposed { get; }
}
