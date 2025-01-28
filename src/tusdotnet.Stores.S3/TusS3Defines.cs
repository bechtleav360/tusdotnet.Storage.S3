namespace tusdotnet.Stores.S3;

/// <summary>
/// Defines constants used by the S3 store.
/// </summary>
public class TusS3Defines
{
    /// <summary>
    /// the definition of a single byte
    /// </summary>
    public const int Byte = 1;
    
    /// <summary>
    /// the number of bytes in a kilobyte
    /// </summary>
    public const int KiloByte = Byte * 1024;
    
    /// <summary>
    /// the number of bytes in a megabyte
    /// </summary>
    public const int MegaByte = KiloByte * 1024;
    
    /// <summary>
    /// the number of bytes in a gigabyte
    /// </summary>
    public const long GigaByte = MegaByte * 1024;
    
    /// <summary>
    /// FileObjectPrefix is prepended to the name of each S3 object that is created
    /// to store uploaded files.
    /// </summary>
    public const string FileObjectPrefix = "files/";
    
    /// <summary>
    /// UploadInfoObjectPrefix is prepended to each <see cref="S3UploadInfo"/> S3
    /// object that is created.
    /// </summary>
    public const string UploadInfoObjectPrefix = "upload-info/";
}