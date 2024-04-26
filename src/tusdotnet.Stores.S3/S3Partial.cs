namespace tusdotnet.Stores.S3;

/// <summary>
/// S3Partial represents a single part of a S3 multipart upload.
/// </summary>
public class S3Partial
{
    public int Number { get; set; }

    public long SizeInBytes { get; set; }

    public string Etag { get; set; } = string.Empty;
    
    public string ChecksumSha1 { get; set; } = string.Empty;
}