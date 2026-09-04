using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Configuration;

namespace Bizden.Infrastructure.PublicAccess;
public interface IObjectStorage { Task<string?> PresignPutAsync(string key, string contentType, CancellationToken ct); Task<bool> VerifyAsync(string key, long size, string contentType, CancellationToken ct); }
public sealed class R2ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3? client; private readonly string? bucket;
    public R2ObjectStorage(IConfiguration config)
    {
        var endpoint = config["R2:Endpoint"]; bucket = config["R2:Bucket"]; var accessKey = config["R2:AccessKeyId"]; var secret = config["R2:SecretAccessKey"];
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(bucket) && !string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secret)) client = new AmazonS3Client(accessKey, secret, new AmazonS3Config { ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "auto" });
    }
    public Task<string?> PresignPutAsync(string key, string contentType, CancellationToken ct) => client is null || bucket is null ? Task.FromResult<string?>(null) : Task.FromResult<string?>(client.GetPreSignedURL(new GetPreSignedUrlRequest { BucketName = bucket, Key = key, Verb = HttpVerb.PUT, ContentType = contentType, Expires = DateTime.UtcNow.AddMinutes(10) }));
    public async Task<bool> VerifyAsync(string key, long size, string contentType, CancellationToken ct)
    {
        if (client is null || bucket is null) return false; try { var result = await client.GetObjectMetadataAsync(bucket, key, ct); return result.ContentLength == size && string.Equals(result.Headers.ContentType, contentType, StringComparison.OrdinalIgnoreCase); } catch (AmazonS3Exception) { return false; }
    }
}
