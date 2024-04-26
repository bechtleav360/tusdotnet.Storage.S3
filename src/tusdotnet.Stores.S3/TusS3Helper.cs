using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace tusdotnet.Stores.S3;

public static class TusS3Helper
{
    internal static string GetFileKey(string key)
    {
        string prefix = TusS3Defines.FileObjectPrefix;

        if (!string.IsNullOrWhiteSpace(prefix) && !prefix.EndsWith("/"))
        {
            prefix += "/";
        }

        return prefix + key;
    }

    internal static string GetUploadInfoKey(string key)
    {
        string prefix = TusS3Defines.UploadInfoObjectPrefix;

        if (!string.IsNullOrWhiteSpace(prefix) && !prefix.EndsWith("/"))
        {
            prefix += "/";
        }

        return prefix + key;
    }
        
    internal static void ToS3MetadataCollection(this MetadataCollection s3MetadataCollection, string tusMetadata)
    {
        if (!string.IsNullOrWhiteSpace(tusMetadata))
        {
            string[] metadataPairs = tusMetadata.Split(',', StringSplitOptions.TrimEntries);

            foreach (string metadataPair in metadataPairs)
            {
                string[] metadataKeyValue = metadataPair.Split(' ');

                if (metadataKeyValue.Any())
                {
                    s3MetadataCollection.Add(metadataKeyValue.ElementAt(0), metadataKeyValue.ElementAtOrDefault(1));
                }
            }
        }
    }
        
    internal static string FromS3MetadataCollection(this MetadataCollection s3MetadataCollection)
    {
        return string.Join(", ", s3MetadataCollection.Keys.Select(key => key + " " + s3MetadataCollection[key]));
    }

    internal static async Task<bool> ObjectExistsAsync(
        this IAmazonS3 client,
        string bucket,
        string key,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.GetObjectMetadataAsync(bucket, key, cancellationToken);

            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
        }

        return false;
    }
        
    internal static async Task DeleteObjectAsync(
        this IAmazonS3 client,
        string bucket,
        string key,
        CancellationToken cancellationToken)
    {
        await client.DeleteObjectAsync(bucket, key, cancellationToken);
    }
}