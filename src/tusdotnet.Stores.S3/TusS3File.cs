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
    private readonly TusS3Api _tusS3Api;

    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TusS3File"/> class.
    /// </summary>
    internal TusS3File(
        string fileId,
        TusS3Api tusS3Api,
        string bucket)
    {
        Id = fileId;
        _tusS3Api = tusS3Api;
        _bucket = bucket;
    }

    /// <inheritdoc />
    public async Task<Stream> GetContentAsync(CancellationToken cancellationToken)
    {
        return await _tusS3Api.GetFileContent(Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, Metadata>> GetMetadataAsync(CancellationToken cancellationToken)
    {
        S3UploadInfo uploadInfo = await _tusS3Api.GetUploadInfo(Id, cancellationToken);

        MetadataParserResult? parsedMetadata =
            MetadataParser.ParseAndValidate(MetadataParsingStrategy.AllowEmptyValues, uploadInfo.Metadata);

        return parsedMetadata.Metadata;
    }
}
