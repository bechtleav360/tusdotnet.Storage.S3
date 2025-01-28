namespace tusdotnet.Stores.S3;

/// <summary>
/// S3Partial represents a single part of a S3 multipart upload.
/// </summary>
public class S3Partial
{
    /// <summary>
    /// the number of the upload part
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// the size of the upload part
    /// </summary>
    public long SizeInBytes { get; set; }

    /// <summary>
    /// the ETag of the upload part
    /// </summary>
    public string Etag { get; set; } = string.Empty;
    
    /// <summary>
    /// the SHA1 checksum of the upload part
    /// </summary>
    public string ChecksumSha1 { get; set; } = string.Empty;
}