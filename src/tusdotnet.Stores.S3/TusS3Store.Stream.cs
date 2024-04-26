using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Stores.S3.Extensions;

namespace tusdotnet.Stores.S3;

public partial class TusS3Store
{
    /// <inheritdoc />
    public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
    {
        _logger.LogTrace("Appending data using the Stream for file '{FileId}'", fileId);

        S3UploadInfo s3UploadInfo = await GetUploadInfo(fileId, cancellationToken);

        if (s3UploadInfo.UploadLength == s3UploadInfo.UploadOffset)
        {
            _logger.LogTrace("Upload length for file '{FileId}' reached, returning", fileId);
            
            await FinalizeUpload(s3UploadInfo, cancellationToken);

            return 0;
        }

        long optimalPartSize = CalculateOptimalPartSize(s3UploadInfo.UploadLength);

        long numberOfBytesReadFromClient;
        var bytesWrittenThisRequest = 0L;

        try
        {
            do
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Stream streamSlice = stream.ReadSlice(optimalPartSize);

                AssertNotToMuchData(s3UploadInfo.UploadOffset, streamSlice.Length, s3UploadInfo.UploadLength);

                _logger.LogDebug("Append '{PartialLength}' bytes to the file '{FileId}'", streamSlice.Length, fileId);

                numberOfBytesReadFromClient = streamSlice.Length;

                bytesWrittenThisRequest += await UploadPartData(s3UploadInfo, streamSlice, cancellationToken);
                
                if (s3UploadInfo.UploadLength == s3UploadInfo.UploadOffset)
                {
                    await FinalizeUpload(s3UploadInfo, cancellationToken);
                }
            }
            while (numberOfBytesReadFromClient != 0);
        }
        catch (Exception ex)
        {
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
}
