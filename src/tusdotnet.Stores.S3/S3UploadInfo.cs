using System;
using System.Collections.Generic;

namespace tusdotnet.Stores.S3;

/// <summary>
/// S3UploadInfo represents the information needed to handle a tus upload in S3.
/// </summary>
public class S3UploadInfo
{
    /// <summary>
    /// Unique id of the file
    /// </summary>
    public string FileId { get; set; } = null!;
    
    /// <summary>
    /// multipartId is the ID given by S3 to us for the multipart upload
    /// </summary>
    public string UploadId { get; set; } = null!;

    /// <summary>
    /// Metadata attached to the file upload
    /// </summary>
    public string Metadata { get; set; } = string.Empty;

    /// <summary>
    /// Total file size in bytes set via the <see cref="TusS3Store.CreateFileAsync"/> call
    /// </summary>
    public long UploadLength { get; set; } = -1;
    
    /// <summary>
    /// Total file size in bytes set via the <see cref="TusS3Store.SetUploadLengthAsync"/> call
    /// </summary>
    public long UploadOffset { get; set; } = 0;
    
    /// <summary>
    /// size of the current upload chunk (needed to verify the completeness of an upload used to verify via checksum)
    /// </summary>
    public long UploadPartSize { get; set; } = -1;

    /// <summary>
    /// Partials of the S3 multipart upload
    /// </summary>
    public List<S3Partial> Parts { get; set; } = new();
    
    /// <summary>
    /// The time when the file expires. Default is 1 day from now (utc).
    /// </summary>
    public DateTimeOffset Expires { get; set; } = DateTimeOffset.UtcNow.AddDays(1);
}