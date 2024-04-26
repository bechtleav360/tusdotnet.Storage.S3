using Amazon.S3;
using Amazon.S3.Model;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Parsers;

namespace tusdotnet.Stores.S3;

/// <summary>
/// Represents a file saved in the TusDiskStore data store
/// </summary>
public class TusS3File : ITusFile
{
    private readonly string _bucket;
    private readonly IAmazonS3 _s3Client;

    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TusS3File"/> class.
    /// </summary>
    internal TusS3File(
        string fileId,
        IAmazonS3 s3Client,
        string bucket)
    {
            Id = fileId;
            _s3Client = s3Client;
            _bucket = bucket;
        }

    /// <inheritdoc />
    public async Task<Stream> GetContentAsync(CancellationToken cancellationToken)
    {
            GetObjectRequest request = new GetObjectRequest()
            {
                BucketName = _bucket,
                Key = TusS3Helper.GetFileKey(Id)
            };

            GetObjectResponse result = await _s3Client.GetObjectAsync(request, cancellationToken);

            return result.ResponseStream;
        }

    /// <inheritdoc />
    public async Task<Dictionary<string, Metadata>> GetMetadataAsync(CancellationToken cancellationToken)
    {
            GetObjectMetadataRequest request = new GetObjectMetadataRequest()
            {
                BucketName = _bucket,
                Key = TusS3Helper.GetFileKey(Id)
            };

            GetObjectMetadataResponse result = await _s3Client.GetObjectMetadataAsync(request, cancellationToken);

            string metadata = result.Metadata.FromS3MetadataCollection();

            MetadataParserResult? parsedMetadata =
                MetadataParser.ParseAndValidate(MetadataParsingStrategy.AllowEmptyValues, metadata);

            return parsedMetadata.Metadata;
        }
}