using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Configuration;

namespace Bizden.Infrastructure.PublicAccess;
public interface IObjectStorage
{
    Task<string?> PresignPutAsync(string key, string contentType, CancellationToken ct);
    Task<string?> PresignGetAsync(string key, string contentType, CancellationToken ct);
    Task<bool> VerifyAsync(string key, long size, string contentType, CancellationToken ct);
    Task<bool> DeleteAsync(string key, CancellationToken ct);
}
public sealed class R2ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3? client; private readonly string? bucket;
    public R2ObjectStorage(IConfiguration config)
    {
        var endpoint = config["R2:Endpoint"]; bucket = config["R2:Bucket"]; var accessKey = config["R2:AccessKeyId"]; var secret = config["R2:SecretAccessKey"];
        AWSConfigsS3.UseSignatureVersion4 = true;
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(bucket) && !string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secret)) client = new AmazonS3Client(accessKey, secret, new AmazonS3Config { ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "auto" });
    }
    public Task<string?> PresignPutAsync(string key, string contentType, CancellationToken ct) => client is null || bucket is null ? Task.FromResult<string?>(null) : Task.FromResult<string?>(client.GetPreSignedURL(new GetPreSignedUrlRequest { BucketName = bucket, Key = key, Verb = HttpVerb.PUT, ContentType = contentType, Expires = DateTime.UtcNow.AddMinutes(10) }));
    public Task<string?> PresignGetAsync(string key, string contentType, CancellationToken ct) => client is null || bucket is null ? Task.FromResult<string?>(null) : Task.FromResult<string?>(client.GetPreSignedURL(new GetPreSignedUrlRequest { BucketName = bucket, Key = key, Verb = HttpVerb.GET, Expires = DateTime.UtcNow.AddMinutes(10), ResponseHeaderOverrides = { ContentType = contentType } }));
    public async Task<bool> VerifyAsync(string key, long size, string contentType, CancellationToken ct)
    {
        if (client is null || bucket is null) return false; try { var result = await client.GetObjectMetadataAsync(bucket, key, ct); return result.ContentLength == size && string.Equals(result.Headers.ContentType, contentType, StringComparison.OrdinalIgnoreCase); } catch (AmazonS3Exception) { return false; }
    }
    public async Task<bool> DeleteAsync(string key, CancellationToken ct)
    {
        if (client is null || bucket is null) return false;
        try { await client.DeleteObjectAsync(bucket, key, ct); return true; }
        catch (AmazonS3Exception) { return false; }
    }
}
