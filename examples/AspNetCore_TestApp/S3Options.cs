namespace AspNetCore_TestApp;

public class S3Options
{
    public string BucketName { get; set; } = null!;
    public string Region { get; set; } = null!;
    public string Endpoint { get; set; } = null!;
    public bool ForcePathStyle { get; set; } = true;
    public string AccessKey { get; set; } = null!;
    public string SecretKey { get; set; } = null!;
}
