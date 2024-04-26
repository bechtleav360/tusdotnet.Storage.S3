using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Stores.FileIdProviders;
using tusdotnet.Stores.S3.Extensions;

namespace tusdotnet.Stores.S3;

/// <summary>
/// TusS3Store provides a storage backend using AWS S3 or compatible servers.
/// </summary>
/// <remarks>
/// In order to allow this backend to function properly, the user accessing the
/// bucket must have at least following AWS IAM policy permissions for the
/// bucket and all of its subresources:
///
/// s3:ListMultipartUploadParts
/// s3:AbortMultipartUpload
/// s3:GetObject
/// s3:PutObject
/// s3:DeleteObject
/// </remarks>
public partial class TusS3Store :
    ITusPipelineStore,
    ITusCreationStore,
    ITusReadableStore,
    ITusTerminationStore,
    ITusExpirationStore,
    ITusCreationDeferLengthStore
{
    private readonly ILogger<TusS3Store> _logger;

    /// <summary>
    /// S3 client instance used to communicate with AWS S3 or compatible servers
    /// </summary>
    private readonly IAmazonS3 _s3Client;

    private readonly TusS3StoreConfiguration _configuration;
    private readonly ITusFileIdProvider _fileIdProvider;
    private static readonly GuidFileIdProvider _defaultFileIdProvider = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TusS3Store"/> class.
    /// </summary>
    /// <param name="logger">Logger to log the execution of the tus protocol</param>
    /// <param name="configuration">The configuration for the TusS3Store</param>
    /// <param name="s3Credentials">Credentials for accessing the S3 service</param>
    /// <param name="s3Config">Configuration for accessing the S3 service</param>
    /// <param name="fileIdProvider">
    ///     The provider that generates ids for files. If unsure use <see cref="GuidFileIdProvider"/>.
    /// </param>
    public TusS3Store(
        ILogger<TusS3Store> logger,
        TusS3StoreConfiguration configuration,
        AWSCredentials s3Credentials,
        AmazonS3Config s3Config,
        ITusFileIdProvider? fileIdProvider = null)
    {
        _logger = logger;
        _configuration = configuration;
        _fileIdProvider = fileIdProvider ?? _defaultFileIdProvider;
        _s3Client = new AmazonS3Client(s3Credentials, s3Config);
    }

    /// <summary>
    /// Calculates the optimal part size in bytes for the s3 multipart upload
    /// </summary>
    /// <param name="totalSize">total size of the file in bytes</param>
    /// <returns>an optimal part size in bytes for the upload</returns>
    private int CalculateOptimalPartSize(long totalSize)
    {
        int optimalPartSize;
        long tmpSize;

        // When upload is smaller or equal to PreferredPartSize, we upload in just one part.
        if (totalSize <= _configuration.PreferredPartSizeInBytes)
        {
            optimalPartSize = _configuration.PreferredPartSizeInBytes;
        }
        // Does the upload fit in MaxMultipartParts parts or less with PreferredPartSize.
        else if (totalSize <= (long)_configuration.PreferredPartSizeInBytes * _configuration.MaxMultipartParts)
        {
            optimalPartSize = _configuration.PreferredPartSizeInBytes;
        }
        // Prerequisite: Be aware, that the result of an integer division (x/y) is
        // ALWAYS rounded DOWN, as there are no digits behind the comma.
        // In order to find out, whether we have an exact result or a rounded down
        // one, we can check, whether the remainder of that division is 0 (x%y == 0).
        //
        // So if the result of (size/MaxMultipartParts) is not a rounded down value,
        // then we can use it as our optimalPartSize. But if this division produces a
        // remainder, we have to round up the result by adding +1. Otherwise, our
        // upload would not fit into MaxMultipartParts number of parts with that
        // size. We would need an additional part in order to upload everything.
        // While in almost all cases, we could skip the check for the remainder and
        // just add +1 to every result, but there is one case, where doing that would
        // doom our upload. When (MaxObjectSize == MaxPartSize * MaxMultipartParts),
        // by adding +1, we would end up with an optimalPartSize > MaxPartSize.
        // With the current S3 API specifications, we will not run into this problem,
        // but these specs are subject to change, and there are other stores as well,
        // which are implementing the S3 API (e.g. RIAK, Ceph RadosGW), but might
        // have different settings.
        else if (totalSize % _configuration.MaxMultipartParts == 0)
        {
            tmpSize = totalSize / _configuration.MaxMultipartParts;
            optimalPartSize = tmpSize > int.MaxValue ? int.MaxValue : (int)tmpSize;
        }
        // Having a remainder larger than 0 means, the float result would have
        // digits after the comma (e.g. be something like 10.9). As a result, we can
        // only squeeze our upload into MaxMultipartParts parts, if we rounded UP
        // this division's result. That is what is happending here. We round up by
        // adding +1, if the prior test for (remainder == 0) did not succeed.
        else
        {
            tmpSize = totalSize / _configuration.MaxMultipartParts + 1;
            optimalPartSize = tmpSize > int.MaxValue ? int.MaxValue : (int)tmpSize;
        }

        // optimalPartSize must never exceed MaxPartSizeInBytes, falling back to PreferredPartSizeInBytes
        if (optimalPartSize > _configuration.MaxPartSizeInBytes)
        {
            optimalPartSize = _configuration.PreferredPartSizeInBytes;
        }

        // optimalPartSize must never undercut MinPartSizeInBytes, falling back to PreferredPartSizeInBytes
        if (optimalPartSize < _configuration.MinPartSizeInBytes)
        {
            optimalPartSize = _configuration.PreferredPartSizeInBytes;
        }

        return optimalPartSize;
    }

    private async Task<string> InitiateUpload(string fileId, string metadata, CancellationToken cancellationToken)
    {
        InitiateMultipartUploadRequest request = new InitiateMultipartUploadRequest
        {
            BucketName = _configuration.BucketName,
            Key = TusS3Helper.GetFileKey(fileId)
        };

        request.Metadata.ToS3MetadataCollection(metadata);

        InitiateMultipartUploadResponse? response =
            await _s3Client.InitiateMultipartUploadAsync(request, cancellationToken);

        _logger.LogDebug(
            "Initiated a new s3 multipart upload for file id '{FileId}' with the upload id '{UploadId}'",
            fileId,
            response.UploadId);

        return response.UploadId;
    }

    private async Task FinalizeUpload(S3UploadInfo uploadInfo, CancellationToken cancellationToken)
    {
        CompleteMultipartUploadRequest request = new CompleteMultipartUploadRequest
        {
            BucketName = _configuration.BucketName,
            Key = TusS3Helper.GetFileKey(uploadInfo.FileId),
            UploadId = uploadInfo.UploadId
        };

        request.AddPartETags(uploadInfo.Parts.OrderBy(p => p.Number).Select(p => new PartETag(p.Number, p.Etag)));

        try
        {
            CompleteMultipartUploadResponse? response =
                await _s3Client.CompleteMultipartUploadAsync(request, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning(
                "Complete the s3 multipart upload for file '{FileId}' with the upload id '{UploadId}' cancelled",
                uploadInfo.FileId,
                uploadInfo.UploadId);
        }

        _logger.LogDebug(
            "Complete the s3 multipart upload for file '{FileId}' with the upload id '{UploadId}'",
            uploadInfo.FileId,
            uploadInfo.UploadId);
    }

    private async Task AbortMultipartUploadAsync(string key, string uploadId, CancellationToken cancellationToken)
    {
        try
        {
            AbortMultipartUploadResponse? response = await _s3Client.AbortMultipartUploadAsync(
                _configuration.BucketName,
                key,
                uploadId,
                cancellationToken);

            if (response != null)
            {
                _logger.LogDebug(
                    "S3 Multipart request for key '{Key}' and upload id '{UploadId}' aborted",
                    key,
                    uploadId);
            }
            else
            {
                _logger.LogWarning(
                    "S3 Multipart request for key '{Key}' and upload id '{UploadId}' not aborted",
                    key,
                    uploadId);
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("S3 Multipart request abortion for upload id '{UploadId}' cancelled", uploadId);
        }
    }

    private async Task<long> UploadPartData(
        S3UploadInfo uploadInfo,
        Stream uploadData,
        CancellationToken cancellationToken)
    {
        int lastPartNumber = uploadInfo.Parts.MaxBy(p => p.Number)?.Number ?? 0;

        S3Partial s3Partial = new S3Partial()
        {
            Number = lastPartNumber + 1,
            SizeInBytes = uploadData.Length
        };

        UploadPartRequest request = new UploadPartRequest()
        {
            BucketName = _configuration.BucketName,
            Key = TusS3Helper.GetFileKey(uploadInfo.FileId),
            UploadId = uploadInfo.UploadId,
            PartNumber = s3Partial.Number,
            InputStream = uploadData
        };

        try
        {
            UploadPartResponse? response = await _s3Client.UploadPartAsync(request, cancellationToken);

            s3Partial.Etag = response.ETag;

            uploadInfo.Parts.Add(s3Partial);
            uploadInfo.UploadOffset += uploadData.Length;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Upload data cancelled info execution for file '{FileId}' cancelled", uploadInfo.FileId);
        }

        await WriteUploadInfo(uploadInfo, cancellationToken);

        return s3Partial.SizeInBytes;
    }

    private async Task WriteUploadInfo(
        S3UploadInfo uploadInfo,
        CancellationToken cancellationToken)
    {
        string uploadInfoJson = JsonSerializer.Serialize(uploadInfo);

        PutObjectRequest request = new PutObjectRequest
        {
            BucketName = _configuration.BucketName,
            Key = TusS3Helper.GetUploadInfoKey(uploadInfo.FileId),
            ContentBody = uploadInfoJson,
            ContentType = "application/json"
        };

        try
        {
            await _s3Client.PutObjectAsync(request, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Writing upload info execution for file '{FileId}' cancelled", uploadInfo.FileId);
        }

        _logger.LogDebug("Wrote upload info for file id '{FileId}'", uploadInfo.FileId);
    }

    private async Task<S3UploadInfo> GetUploadInfo(
        string fileId,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("Request to retrieve the upload info for file id '{FileId}'", fileId);

        GetObjectRequest request = new GetObjectRequest()
        {
            BucketName = _configuration.BucketName,
            Key = TusS3Helper.GetUploadInfoKey(fileId)
        };

        S3UploadInfo? uploadInfo = null;

        try
        {
            GetObjectResponse? response = await _s3Client.GetObjectAsync(request, cancellationToken);

            uploadInfo = await JsonSerializer.DeserializeAsync<S3UploadInfo>(
                response.ResponseStream,
                cancellationToken: cancellationToken);

            if (uploadInfo == null)
            {
                _logger.LogWarning("No upload info found file '{FileId}'", fileId);
            }
            else
            {
                _logger.LogDebug("Upload info acquired from storage for file '{FileId}'", fileId);
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Acquiring of the upload info file '{FileId}' cancelled", fileId);
        }

        return uploadInfo ?? throw new TusStoreException($"No s3 upload info found for file id '{fileId}'");
    }

    private async Task<IEnumerable<S3UploadInfo>> GetAllUploadInfos(CancellationToken cancellationToken)
    {
        _logger.LogTrace("Request to retrieve all upload infos");

        IListObjectsV2Paginator? paginator = _s3Client.Paginators.ListObjectsV2(
            new ListObjectsV2Request
            {
                BucketName = _configuration.BucketName,
                Prefix = TusS3Defines.UploadInfoObjectPrefix,
                Delimiter = "/"
            });

        List<S3UploadInfo> uploadInfos = new List<S3UploadInfo>();

        await foreach (ListObjectsV2Response? response in paginator.Responses.WithCancellation(cancellationToken))
        {
            foreach (S3Object s3Object in response.S3Objects)
            {
                string fileId = s3Object.Key.Split("/").Last().Trim();

                try
                {
                    uploadInfos.Add(await GetUploadInfo(fileId, cancellationToken));
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        _logger.LogDebug("Retrieved {UploadInfosCount} upload infos from the storage backend", uploadInfos.Count);

        return uploadInfos;
    }

    /// <inheritdoc />
    public async Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken)
    {
        _logger.LogTrace("Checking existence of file '{FileId}'", fileId);

        bool fileExists = await _s3Client.ObjectExistsAsync(
            _configuration.BucketName,
            TusS3Helper.GetUploadInfoKey(fileId),
            cancellationToken);

        if (fileExists)
        {
            _logger.LogTrace("File for '{FileId}' found, returning file handle", fileId);
        }
        else
        {
            _logger.LogWarning("File for '{FileId}' not found", fileId);
        }

        return fileExists;
    }

    /// <inheritdoc />
    public async Task<long?> GetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
    {
        S3UploadInfo uploadInfo = await GetUploadInfo(fileId, cancellationToken);

        _logger.LogDebug(
            "Returning requested UploadLength with value '{UploadLength}' for file id '{FileId}'",
            uploadInfo.UploadLength,
            fileId);

        return uploadInfo.UploadLength;
    }

    /// <inheritdoc />
    public async Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken)
    {
        S3UploadInfo uploadInfo = await GetUploadInfo(fileId, cancellationToken);

        _logger.LogDebug(
            "Returning requested UploadOffset with value '{UploadOffset}' for file id '{FileId}'",
            uploadInfo.UploadOffset,
            fileId);

        return uploadInfo.UploadOffset;
    }

    /// <inheritdoc />
    public async Task<string> CreateFileAsync(
        long uploadLength,
        string metadata,
        CancellationToken cancellationToken)
    {
        string fileId = await _fileIdProvider.CreateId(metadata);
        string uploadId = await InitiateUpload(fileId, metadata, cancellationToken);

        S3UploadInfo uploadInfo = new S3UploadInfo
        {
            FileId = fileId,
            UploadId = uploadId,
            UploadLength = uploadLength
        };

        await WriteUploadInfo(uploadInfo, cancellationToken);

        _logger.LogDebug(
            "Created a new file reference with file id '{FileId}' and length '{UploadLength}'",
            fileId,
            uploadLength);

        return fileId;
    }

    /// <inheritdoc />
    public async Task<string> GetUploadMetadataAsync(string fileId, CancellationToken cancellationToken)
    {
        GetObjectMetadataRequest request = new GetObjectMetadataRequest()
        {
            BucketName = _configuration.BucketName,
            Key = TusS3Helper.GetFileKey(fileId)
        };

        GetObjectMetadataResponse result = await _s3Client.GetObjectMetadataAsync(request, cancellationToken);

        _logger.LogDebug("UploadMetadata for file id '{FileId}' requested, returning data", fileId);

        return result.Metadata.FromS3MetadataCollection();
    }

    /// <inheritdoc />
    public async Task<ITusFile?> GetFileAsync(string fileId, CancellationToken cancellationToken)
    {
        bool fileExists = await FileExistAsync(fileId, cancellationToken);

        if (fileExists)
        {
            _logger.LogDebug("GetFile for file id '{FileId}' requested, returning file handle", fileId);
        }
        else
        {
            _logger.LogWarning("GetFile for file id '{FileId}' not found", fileId);
        }

        return fileExists
            ? new TusS3File(fileId, _s3Client, _configuration.BucketName)
            : null;
    }

    /// <inheritdoc />
    public async Task DeleteFileAsync(string fileId, CancellationToken cancellationToken)
    {
        bool fileExists = await FileExistAsync(fileId, cancellationToken);

        if (fileExists)
        {
            S3UploadInfo uploadInfo = await GetUploadInfo(fileId, cancellationToken);

            Task.WaitAll(
                AbortMultipartUploadAsync(
                    TusS3Helper.GetFileKey(fileId),
                    uploadInfo.UploadId,
                    cancellationToken),
                _s3Client.DeleteObjectAsync(
                    _configuration.BucketName,
                    TusS3Helper.GetFileKey(fileId),
                    cancellationToken),
                _s3Client.DeleteObjectAsync(
                    _configuration.BucketName,
                    TusS3Helper.GetUploadInfoKey(fileId),
                    cancellationToken));

            _logger.LogDebug(
                "File with file id '{FileId}' deleted",
                fileId);
        }
        else
        {
            _logger.LogWarning(
                "Deletion for non existing file id '{FileId}' ignored",
                fileId);
        }
    }

    /// <inheritdoc />
    public async Task SetExpirationAsync(string fileId, DateTimeOffset expires, CancellationToken cancellationToken)
    {
        S3UploadInfo uploadInfo = await GetUploadInfo(fileId, cancellationToken);
        uploadInfo.Expires = expires;
        await WriteUploadInfo(uploadInfo, cancellationToken);

        _logger.LogDebug(
            "Expiration for file id '{FileId}' set to value '{FileExpires}'",
            fileId,
            uploadInfo.Expires);
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetExpirationAsync(string fileId, CancellationToken cancellationToken)
    {
        S3UploadInfo uploadInfo = await GetUploadInfo(fileId, cancellationToken);

        _logger.LogDebug(
            "Expiration for file id '{FileId}' has value '{FileExpires}'",
            fileId,
            uploadInfo.Expires);

        return uploadInfo.Expires;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken cancellationToken)
    {
        IEnumerable<S3UploadInfo> uploadInfos = await GetAllUploadInfos(cancellationToken);

        IEnumerable<S3UploadInfo> expiredIncompleteFiles =
            uploadInfos.Where(info => info.Expires.HasPassed() && info.UploadOffset < info.UploadLength);

        return expiredIncompleteFiles.Select(info => info.FileId);
    }

    /// <inheritdoc />
    public async Task<int> RemoveExpiredFilesAsync(CancellationToken cancellationToken)
    {
        IEnumerable<S3UploadInfo> uploadInfos = await GetAllUploadInfos(cancellationToken);

        IListMultipartUploadsPaginator? paginator = _s3Client.Paginators.ListMultipartUploads(
            new ListMultipartUploadsRequest()
            {
                BucketName = _configuration.BucketName,
                Prefix = TusS3Defines.FileObjectPrefix,
                Delimiter = "/"
            });

        if (paginator != null)
        {
            IEnumerable<string> knownUploadIds = uploadInfos.Select(info => info.UploadId);

            await foreach (var response in paginator.Responses.WithCancellation(cancellationToken))
            {
                IEnumerable<MultipartUpload> unattachedMultipartUploads =
                    response.MultipartUploads.Where(upload => !knownUploadIds.Contains(upload.UploadId));
                
                foreach (MultipartUpload unattachedMultipartUpload in unattachedMultipartUploads)
                {
                    await AbortMultipartUploadAsync(
                        unattachedMultipartUpload.Key,
                        unattachedMultipartUpload.UploadId,
                        cancellationToken);
                }
            }
        }

        IEnumerable<S3UploadInfo> expiredIncompleteFiles =
            uploadInfos.Where(info => info.Expires.HasPassed() && info.UploadOffset < info.UploadLength);

        foreach (S3UploadInfo uploadInfo in expiredIncompleteFiles)
        {
            await DeleteFileAsync(uploadInfo.FileId, cancellationToken);

            _logger.LogTrace("Deleted incomplete expired file with id '{FileId}'", uploadInfo.FileId);
        }

        return expiredIncompleteFiles.Count();
    }

    /// <inheritdoc />
    public async Task SetUploadLengthAsync(string fileId, long uploadLength, CancellationToken cancellationToken)
    {
        S3UploadInfo uploadInfo = await GetUploadInfo(fileId, cancellationToken);
        uploadInfo.UploadLength = uploadLength;
        await WriteUploadInfo(uploadInfo, cancellationToken);

        _logger.LogDebug(
            "Upload length '{UploadLength}' received & stored for file id '{FileId}'",
            uploadLength,
            fileId);
    }

    private static void AssertNotToMuchData(
        long uploadOffsetLength,
        long numberOfBytesReadFromClient,
        long? fileUploadLengthProvidedDuringCreate)
    {
        long requestDataLength = uploadOffsetLength + numberOfBytesReadFromClient;

        if (requestDataLength > fileUploadLengthProvidedDuringCreate)
        {
            throw new TusStoreException(
                $"Request contains more data than the file's upload length. "
                + $"Request data: {requestDataLength}, upload length: {fileUploadLengthProvidedDuringCreate}.");
        }
    }
}
