using System;
using System.Collections.Generic;

namespace tusdotnet.Stores.S3;

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
    /// Total file size in bytes set via the <see cref="TusS3Store.CreateFileAsync"/> call
    /// </summary>
    public long UploadLength { get; set; } = -1;
    
    /// <summary>
    /// Total file size in bytes set via the <see cref="TusS3Store.SetUploadLengthAsync"/> call
    /// </summary>
    public long UploadOffset { get; set; } = 0;
    
    /// <summary>
    /// Value of the Upload-Concat header set when <see cref="TusS3Store.CreatePartialFileAsync"/>
    /// or <see cref="TusS3Store.CreatePartialFileAsync"/> was used to create the file
    /// </summary>
    public string? UploadConcatHeader { get; set; }
    
    /// <summary>
    /// size of the current upload chunk (needed to verify the completeness of an upload used to verify via checksum)
    /// </summary>
    public long UploadPartSize { get; set; } = -1;
    
    /// <summary>
    /// 
    /// </summary>
    public long UploadPartOffset { get; set; } = -1;

    /// <summary>
    /// Partials of the S3 multipart upload
    /// </summary>
    public List<S3Partial> Parts { get; set; } = new();
    
    public DateTimeOffset Expires { get; set; } = DateTimeOffset.UtcNow.AddDays(1);
}