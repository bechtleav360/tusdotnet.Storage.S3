using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using NSubstitute;
using tusdotnet.Interfaces;

namespace tusdotnet.Stores.S3.Tests;

public class TusS3StoreTests
{
    [Fact]
    public async Task Can_Upload_File()
    {
        // Arrange

        var logger = Substitute.For<ILogger<TusS3Store>>();
        var tusConfig = new TusS3StoreConfiguration
        {
            BucketName = "SomeBucket",
            UploadInfoObjectPrefix = "infoFolder/",
            FileObjectPrefix = "fileFolder/"
        };
        var s3Client = Substitute.For<IAmazonS3>();
        s3Client.InitiateMultipartUploadAsync(Arg.Any<InitiateMultipartUploadRequest>(),Arg.Any<CancellationToken>()).Returns(Task.FromResult(new InitiateMultipartUploadResponse { UploadId = "42"}));
        var fileIdProvider = Substitute.For<ITusFileIdProvider>();
        fileIdProvider.CreateId(Arg.Any<string>())
            .Returns<Task<string>>(args => Task.FromResult($"{((string)args[0]).Length}{args[0]}"));
        
        var tusS3Store = new TusS3Store(logger, tusConfig, s3Client, fileIdProvider);

        // Act
        var response = await tusS3Store.CreateFileAsync(1024, "some metadata", CancellationToken.None);

        // Assert
        await s3Client.Received(1).InitiateMultipartUploadAsync(
            Arg.Is<InitiateMultipartUploadRequest>(request =>
                request.BucketName == "SomeBucket" && request.Key == "fileFolder/13some metadata")
            , Arg.Any<CancellationToken>());
        await s3Client.Received(1).PutObjectAsync(Arg.Is<PutObjectRequest>(request => request.BucketName == "SomeBucket" && request.Key == "infoFolder/13some metadata"), Arg.Any<CancellationToken>());
        Assert.Equal("13some metadata",response);
    }
}