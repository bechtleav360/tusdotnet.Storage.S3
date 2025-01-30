# tusdotnet.Stores.S3

[![Nuget](https://img.shields.io/nuget/v/tusdotnet.Stores.S3)](https://www.nuget.org/packages/tusdotnet.Stores.S3)

The Package tusdotnet.Stores.S3 implements all necessary interfaces to use S3 as a file storage backend.
The Implementation is put into the TusS3Store class

# What is tus?
[Tus](https://tus.io/) is a web based protocol for resumable uploads.  Implementations and client libraries exist for many platforms.

# What is tusdotnet?
[tusdotnet](https://github.com/tusdotnet/tusdotnet) is a popular implementation of the tus protocol for .net.

# Why do I need tusdotnet.Stores.S3?
[Tusdotnet](https://github.com/tusdotnet/tusdotnet) only comes with a disk storage implementation.  This extension allows you to use s3 blobstorage instead of local (or network attached) disk.

# Implemented Extensions
The tus protocol offers a few [extensions](https://tus.io/protocols/resumable-upload.html#protocol-extensions).  The following extensions are implemented:
* Termination - Allows for deletion of completed and incomplete uploads.
* Expiration - Server will consider uploads past a certain time expired and ready for deletion.
* Pipelines - more efficient than handling plain streams   
      
# Configuration

In order to allow this backend to function properly, the user accessing the
bucket must have at least following AWS IAM policy permissions for the
bucket and all of its subresources:

```text
s3:AbortMultipartUpload
s3:DeleteObject
s3:GetObject
s3:ListMultipartUploadParts
s3:PutObject
```

While this package uses the official AWS SDK for Go, S3Store is able
to work with any S3-compatible service such as MinIO. In order to change
the HTTP endpoint used for sending requests to, adjust the `BaseEndpoint`
option in the AWSSDK.S3 nuget package (https://aws.amazon.com/sdk-for-net/).

# Implementation

Once a new tus upload is initiated, multiple objects in S3 are created:

First of all, a new info object is stored which contains a JSON-encoded blob
of general information about the upload including its size and meta data.
This kind of objects have the suffix ".info" in their key.

In addition a new multipart upload (http://docs.aws.amazon.com/AmazonS3/latest/dev/uploadobjusingmpu.html) is
created. Whenever a new chunk is uploaded to tus using a PATCH request, a
new part is pushed to the multipart upload on S3.

If meta data is associated with the upload during creation, it will be added
to the multipart upload and after finishing it, the meta data will be passed
to the final object.

Once the upload is finished, the multipart upload is completed, resulting in
the entire file being stored in the bucket. The info object, containing
meta data is not deleted. It is recommended to copy the finished upload to
another bucket to avoid it being deleted by the Termination extension.

If an upload is about to being terminated, the multipart upload is aborted
which removes all of the uploaded parts from the bucket. In addition, the
info object is also deleted. If the upload has been finished already, the
finished object containing the entire upload is also removed.

# Considerations

In order to support tus' principle of resumable upload, S3's Multipart-Uploads
are internally used.

In addition, it must be mentioned that AWS S3 only offers eventual
consistency (https://docs.aws.amazon.com/AmazonS3/latest/dev/Introduction.html#ConsistencyModel).
Therefore, it is required to build additional measurements in order to
prevent concurrent access to the same upload resources which may result in
data corruption.

# Usage

```csharp
ILogger<TusS3Store> logger;

var tusS3StoreConfig = new TusS3StoreConfiguration()
{
    BucketName = options.Value.BucketName
};

var awsCredentials = new BasicAWSCredentials("myaccessKey", "mysecretkey");

AmazonS3Config s3Config = new AmazonS3Config
{
    // MUST set this before setting ServiceURL and it should match the `MINIO_REGION` environment variable
    AuthenticationRegion = "us-east-1",
    // MUST be true to work correctly with MinIO server
    ServiceURL = "https://mys3endpoint.com",
    ForcePathStyle = true,
    // see changes in AWSSDK.S3 https://github.com/aws/aws-sdk-net/issues/3610
    // set RequestChecksumCalculation to "WHEN_REQUIRED" if using alternative S3 implementation like minio/ceph etc
    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED
};

var tusStore = new TusS3Store(logger, tusS3StoreConfig, awsCredentials, s3Config);

var tusConfig = new DefaultTusConfiguration
{
    Store = CreateTusS3Store(services),
    MetadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues,
    UsePipelinesIfAvailable = true,
    // Set an expiration time, where incomplete files can no longer be updated.
    // This value can either be absolute or sliding.
    // Absolute expiration will be saved per file on create
    // Sliding expiration will be saved per file on create and updated on each patch/update.
    Expiration = new SlidingExpiration(TimeSpan.FromMinutes(1))
};

```

# Known issues

## Amazon. S3 .AmazonSException: The Content-SHA256 you specified did not match what we received

If you are using an S3 compatible backend you might encounter this error message. 
This is caused by a change in the AWSSDK.S3 default behaviour see https://github.com/aws/aws-sdk-net/issues/3610                   

It can be mitigated by setting the `RequestChecksumCalculation` to `RequestChecksumCalculation.WHEN_REQUIRED` in the `AmazonS3Config`.
                                                       

# Special Thanks
- [Bechtle A/V Software Solutions 360°](https://av360.io/) for sponsoring the project
- [Xtensible.TusDotNet.Azure](https://github.com/giometrix/Xtensible.TusDotNet.Azure) for a boilerplate to create this extension