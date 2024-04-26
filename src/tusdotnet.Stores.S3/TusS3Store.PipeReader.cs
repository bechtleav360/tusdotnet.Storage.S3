using Microsoft.Extensions.Logging;
using System;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Stores.S3.Extensions;

namespace tusdotnet.Stores.S3;

public partial class TusS3Store
{
    /// <inheritdoc />
    public async Task<long> AppendDataAsync(string fileId, PipeReader reader, CancellationToken cancellationToken)
    {
        _logger.LogTrace("Appending data using the PipeReader for file '{FileId}'", fileId);

        S3UploadInfo s3UploadInfo = await GetUploadInfo(fileId, cancellationToken);

        if (s3UploadInfo.UploadLength == s3UploadInfo.UploadOffset)
        {
            _logger.LogTrace("Upload length for file '{FileId}' reached, returning", fileId);

            await FinalizeUpload(s3UploadInfo, cancellationToken);
            
            return 0;
        }

        ReadResult result = default;
        var bytesWrittenThisRequest = 0L;

        try
        {
            long optimalPartSize = CalculateOptimalPartSize(s3UploadInfo.UploadLength);

            while (!PipeReadingIsDone(result, cancellationToken))
            {
                result = await reader.ReadAtLeastAsync((int)optimalPartSize, cancellationToken);

                AssertNotToMuchData(s3UploadInfo.UploadOffset, result.Buffer.Length, s3UploadInfo.UploadLength);

                bytesWrittenThisRequest += await UploadPartData(
                    s3UploadInfo,
                    result.Buffer.AsStream(),
                    cancellationToken);
                
                if (s3UploadInfo.UploadLength == s3UploadInfo.UploadOffset)
                {
                    await FinalizeUpload(s3UploadInfo, cancellationToken);
                }

                _logger.LogDebug("Append '{PartialLength}' bytes to the file '{FileId}'", result.Buffer.Length, fileId);

                reader.AdvanceTo(result.Buffer.End);
            }

            await reader.CompleteAsync();
        }
        catch (Exception ex)
        {
            // Clear memory and complete the reader to not cause a
            // Microsoft.AspNetCore.Connections.ConnectionAbortedException inside Kestrel
            // later on as this is an "expected" exception.
            try
            {
                reader.AdvanceTo(result.Buffer.End);
                await reader.CompleteAsync();
            }
            catch
            {
                /* Ignore if we cannot complete the reader so that the real exception will propagate. */
            }
            
            if (ex is OperationCanceledException or TaskCanceledException)
            {
                _logger.LogWarning("Cancelled the upload operation for file id '{FileId}'", fileId);
            }
            else
            {
                throw;
            }
        }

        return bytesWrittenThisRequest;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PipeReadingIsDone(ReadResult result, CancellationToken cancellationToken)
    {
        return cancellationToken.IsCancellationRequested || result.IsCanceled || result.IsCompleted;
    }
}
