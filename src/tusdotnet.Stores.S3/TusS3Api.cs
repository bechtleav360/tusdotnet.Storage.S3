using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace tusdotnet.Stores.S3;

internal class TusS3Api
{
    private readonly ILogger _logger;
    private readonly IAmazonS3 _s3Client;
    private readonly TusS3BucketConfiguration _bucketConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="TusS3Api"/> class.
    /// </summary>
    /// <param name="logger">Logger to log the execution of the tus protocol</param>
    /// <param name="s3Client">S3 client instance used to interact with the s3 bucket</param>
    /// <param name="bucketConfiguration">Bucket and file object prefix configuration</param>
    public TusS3Api(ILogger logger,
        IAmazonS3 s3Client,
        TusS3BucketConfiguration bucketConfiguration
    )
    {
        _logger = logger;
        _s3Client = s3Client;
        _bucketConfiguration = bucketConfiguration;
    }

    private string GetFileKey(string key)
    {
        return _bucketConfiguration.FileObjectPrefix + key;
    }

    private string GetUploadInfoKey(string key)
    {
        return _bucketConfiguration.UploadInfoObjectPrefix + key;
    }

    internal async Task<bool> TusFileExists(
        string fileId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(_bucketConfiguration.BucketName, GetUploadInfoKey(fileId), cancellationToken);

            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // ignore since we just need to check the status code since the sdk has no better way for this...
        }

        return false;
    }

    internal async Task DeleteFile(
        string fileId,
        CancellationToken cancellationToken)
    {
        string key = GetFileKey(fileId);
        await _s3Client.DeleteObjectAsync(_bucketConfiguration.BucketName, key, cancellationToken);
    }

    internal async Task DeleteUploadInfo(
        string fileId,
        CancellationToken cancellationToken)
    {
        string key = GetUploadInfoKey(fileId);
        await _s3Client.DeleteObjectAsync(_bucketConfiguration.BucketName, key, cancellationToken);
    }

    internal async Task<string> InitiateUpload(string fileId, CancellationToken cancellationToken)
    {
        InitiateMultipartUploadRequest request = new InitiateMultipartUploadRequest
        {
            BucketName = _bucketConfiguration.BucketName,
            Key = GetFileKey(fileId)
        };

        InitiateMultipartUploadResponse? response =
            await _s3Client.InitiateMultipartUploadAsync(request, cancellationToken);

        _logger.LogDebug(
            "Initiated a new s3 multipart upload for file id '{FileId}' with the upload id '{UploadId}'",
            fileId,
            response.UploadId);

        return response.UploadId;
    }

    internal async Task FinalizeUpload(S3UploadInfo uploadInfo, CancellationToken cancellationToken)
    {
        CompleteMultipartUploadRequest request = new CompleteMultipartUploadRequest
        {
            BucketName = _bucketConfiguration.BucketName,
            Key = GetFileKey(uploadInfo.FileId),
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

    internal async Task AbortMultipartUploadAsync(string fileId, string uploadId, CancellationToken cancellationToken)
    {
        try
        {
            string key = GetFileKey(fileId);

            AbortMultipartUploadResponse? response = await _s3Client.AbortMultipartUploadAsync(
                _bucketConfiguration.BucketName,
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

    internal async Task<long> UploadPartData(
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
            BucketName = _bucketConfiguration.BucketName,
            Key = GetFileKey(uploadInfo.FileId),
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

    internal async Task WriteUploadInfo(
        S3UploadInfo uploadInfo,
        CancellationToken cancellationToken)
    {
        string uploadInfoJson = JsonSerializer.Serialize(uploadInfo);

        PutObjectRequest request = new PutObjectRequest
        {
            BucketName = _bucketConfiguration.BucketName,
            Key = GetUploadInfoKey(uploadInfo.FileId),
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

    internal async Task<S3UploadInfo> GetUploadInfo(
        string fileId,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("Request to retrieve the upload info for file id '{FileId}'", fileId);

        GetObjectRequest request = new GetObjectRequest()
        {
            BucketName = _bucketConfiguration.BucketName,
            Key = GetUploadInfoKey(fileId)
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

    internal async Task<IEnumerable<S3UploadInfo>> GetAllUploadInfos(CancellationToken cancellationToken)
    {
        _logger.LogTrace("Request to retrieve all upload infos");

        IListObjectsV2Paginator? paginator = _s3Client.Paginators.ListObjectsV2(
            new ListObjectsV2Request
            {
                BucketName = _bucketConfiguration.BucketName,
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

    internal async Task<Stream> GetFileContent(
        string fileId,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("Request to retrieve the contents for file id '{FileId}'", fileId);

        GetObjectRequest request = new GetObjectRequest()
        {
            BucketName = _bucketConfiguration.BucketName,
            Key = GetFileKey(fileId)
        };

        GetObjectResponse result = await _s3Client.GetObjectAsync(request, cancellationToken);

        return result.ResponseStream;
    }

    internal IListMultipartUploadsPaginator? ListMultipartUploads()
    {
        return _s3Client.Paginators.ListMultipartUploads(
            new ListMultipartUploadsRequest()
            {
                BucketName = _bucketConfiguration.BucketName,
                Prefix = TusS3Defines.FileObjectPrefix,
                Delimiter = "/"
            });
    }
}

internal record TusS3BucketConfiguration(string BucketName, string UploadInfoObjectPrefix, string FileObjectPrefix);