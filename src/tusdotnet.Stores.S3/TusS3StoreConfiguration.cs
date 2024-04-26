namespace tusdotnet.Stores.S3;

public class TusS3StoreConfiguration
{
    /// <summary>
    /// Bucket used to store the data in
    /// </summary>
    public string BucketName { get; set; } = null!;

    /// <summary>
    /// MaxPartSize specifies the maximum size of a single part uploaded to S3
    /// in bytes. This value must be bigger than MinPartSize! In order to
    /// choose the correct number, two things have to be kept in mind:
    ///
    /// If this value is too big and uploading the part to S3 is interrupted
    /// expectedly, the entire part is discarded and the end user is required
    /// to resume the upload and re-upload the entire big part. In addition, the
    /// entire part must be written to disk before submitting to S3.
    ///
    /// If this value is too low, a lot of requests to S3 may be made, depending
    /// on how fast data is coming in. This may result in an eventual overhead.
    ///
    /// Default: 5GB
    /// </summary>
    public long MaxPartSizeInBytes { get; set; } = 5 * TusS3Defines.GigaByte;

    /// <summary>
    /// MinPartSize specifies the minimum size of a single part uploaded to S3
    /// in bytes. This number needs to match with the underlying S3 backend or else
    /// uploaded parts will be rejected. AWS S3, for example, uses 5MB for this value.
    ///
    /// Default: 5MB
    /// </summary>
    public int MinPartSizeInBytes { get; set; } = 5 * TusS3Defines.MegaByte;

    /// <summary>
    /// PreferredPartSize specifies the preferred size of a single part uploaded to
    /// S3. TusS3Store will attempt to slice the incoming data into parts with this
    /// size whenever possible. In some cases, smaller parts are necessary, so
    /// not every part may reach this value. The <see cref="PreferredPartSizeInBytes"/> must be inside the
    /// range of <see cref="MinPartSizeInBytes"/> to <see cref="MaxPartSizeInBytes"/>.
    ///
    /// Default: 50MB
    /// </summary>
    public int PreferredPartSizeInBytes { get; set; } = 50 * TusS3Defines.MegaByte;

    /// <summary>
    /// MaxMultipartParts is the maximum number of parts an S3 multipart upload is
    /// allowed to have according to AWS S3 API specifications.
    /// See: http://docs.aws.amazon.com/AmazonS3/latest/dev/qfacts.html
    ///
    /// Default: 1000
    /// </summary>
    public int MaxMultipartParts { get; set; } = 1_000;

    /// <summary>
    /// Limits on how many concurrent part uploads to S3 are allowed.
    /// </summary>
    public int ConcurrentUploadLimit { get; set; } = 10;
}
