using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using NSubstitute;
using tusdotnet.Interfaces;

namespace tusdotnet.Stores.S3.Tests;

public class TusS3StoreTests
{
    private const string BucketName = "SomeBucket";

    private (IAmazonS3, TusS3Store) GetStoreSubstitute(string uploadInfoObjectPrefix, string fileObjectPrefix)
    {
        var logger = Substitute.For<ILogger<TusS3Store>>();

        var tusConfig = new TusS3StoreConfiguration
        {
            BucketName = BucketName,
            UploadInfoObjectPrefix = uploadInfoObjectPrefix,
            FileObjectPrefix = fileObjectPrefix
        };

        var s3Client = Substitute.For<IAmazonS3>();

        s3Client.InitiateMultipartUploadAsync(Arg.Any<InitiateMultipartUploadRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(
                    new InitiateMultipartUploadResponse
                    {
                        UploadId = "42"
                    }));

        var fileIdProvider = Substitute.For<ITusFileIdProvider>();

        fileIdProvider.CreateId(Arg.Any<string>())
            .Returns<Task<string>>(args => Task.FromResult($"{((string)args[0]).Length}{args[0]}"));

        return (s3Client, new TusS3Store(logger, tusConfig, s3Client, fileIdProvider));
    }
    
    [Fact]
    public async Task Can_Upload_File()
    {
        (string, string)[] prefixTuples =
        [
            ("infoFolder/", "fileFolder/"),
            ("infoFolder/", "")
        ];

        foreach (var (uploadInfoObjectPrefix, fileObjectPrefix) in prefixTuples)
        {
            // Arrange
            var (s3Client, tusS3Store) = GetStoreSubstitute(uploadInfoObjectPrefix, fileObjectPrefix);

            // Act
            var response = await tusS3Store.CreateFileAsync(1024, "some metadata", CancellationToken.None);

            // Assert
            const string expectedFileKey = "13some metadata";
            
            await s3Client.Received(1)
                .InitiateMultipartUploadAsync(
                    Arg.Is<InitiateMultipartUploadRequest>(request =>
                        request.BucketName == "SomeBucket" && request.Key == Path.Combine(fileObjectPrefix, expectedFileKey)),
                    Arg.Any<CancellationToken>());

            await s3Client.Received(1)
                .PutObjectAsync(
                    Arg.Is<PutObjectRequest>(request =>
                        request.BucketName == "SomeBucket" && request.Key == Path.Combine(uploadInfoObjectPrefix, expectedFileKey)),
                    Arg.Any<CancellationToken>());

            Assert.Equal(expectedFileKey, response);
        }
    }

    [Fact]
    public async Task CanRemoveExpiredFiles()
    {
        const string uploadInfoObjectPrefix = "uploadInfoPrefix/";
        const string fileObjectPrefix = "filePrefix/";
        
        var (s3Client, tusS3Store) = GetStoreSubstitute(uploadInfoObjectPrefix, fileObjectPrefix);

        var response = await tusS3Store.RemoveExpiredFilesAsync(CancellationToken.None);
        
        s3Client.Paginators.Received(1)
            .ListObjectsV2(Arg.Is<ListObjectsV2Request>(request => request.BucketName == "SomeBucket" && request.Prefix == uploadInfoObjectPrefix));
        
        s3Client.Paginators.Received(1)
            .ListMultipartUploads(Arg.Is<ListMultipartUploadsRequest>(request => request.BucketName == "SomeBucket" && request.Prefix == fileObjectPrefix));
        
        Assert.Equal(0, response);
    }

    [Fact]
    public void CantUseEqualObjectPrefix()
    {
        var logger = Substitute.For<ILogger<TusS3Store>>();
        
        var tusConfig = new TusS3StoreConfiguration
        {
            BucketName = BucketName,
            UploadInfoObjectPrefix = "EQUAL",
            FileObjectPrefix = "EQUAL"
        };
        
        var s3Client = Substitute.For<IAmazonS3>();

        Assert.Throws<ArgumentException>(() => new TusS3Store(logger, tusConfig, s3Client));
    }
}
