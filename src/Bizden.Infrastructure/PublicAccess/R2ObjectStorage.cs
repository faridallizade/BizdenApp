using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Bizden.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;

namespace Bizden.Infrastructure.PublicAccess;
public interface IObjectStorage
{
    Task<string?> PresignPutAsync(string key, string contentType, CancellationToken ct);
    Task<string?> PresignGetAsync(string key, string contentType, CancellationToken ct);
    Task<bool> VerifyAsync(string key, long size, string contentType, CancellationToken ct);
    Task<bool> DeleteAsync(string key, CancellationToken ct);
    Task<byte[]?> DownloadAsync(string key, CancellationToken ct);
    Task<bool> UploadAsync(string key, string contentType, byte[] content, CancellationToken ct);
}
public sealed class R2ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3? client; private readonly string? bucket; private readonly RuntimeMetrics metrics;
    public R2ObjectStorage(IConfiguration config, RuntimeMetrics metrics)
    {
        this.metrics = metrics;
        var endpoint = config["R2:Endpoint"]; bucket = config["R2:Bucket"]; var accessKey = config["R2:AccessKeyId"]; var secret = config["R2:SecretAccessKey"];
        AWSConfigsS3.UseSignatureVersion4 = true;
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(bucket) && !string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secret)) client = new AmazonS3Client(accessKey, secret, new AmazonS3Config { ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "auto" });
    }
    public Task<string?> PresignPutAsync(string key, string contentType, CancellationToken ct) => client is null || bucket is null ? Task.FromResult<string?>(null) : Task.FromResult<string?>(client.GetPreSignedURL(new GetPreSignedUrlRequest { BucketName = bucket, Key = key, Verb = HttpVerb.PUT, ContentType = contentType, Expires = DateTime.UtcNow.AddMinutes(10) }));
    public Task<string?> PresignGetAsync(string key, string contentType, CancellationToken ct) => client is null || bucket is null ? Task.FromResult<string?>(null) : Task.FromResult<string?>(client.GetPreSignedURL(new GetPreSignedUrlRequest { BucketName = bucket, Key = key, Verb = HttpVerb.GET, Expires = DateTime.UtcNow.AddMinutes(10), ResponseHeaderOverrides = { ContentType = contentType } }));
    public async Task<bool> VerifyAsync(string key, long size, string contentType, CancellationToken ct)
    {
        if (client is null || bucket is null) return false;
        try
        {
            var metadata = await client.GetObjectMetadataAsync(bucket, key, ct);
            if (metadata.ContentLength != size || !string.Equals(metadata.Headers.ContentType, contentType, StringComparison.OrdinalIgnoreCase)) return false;
            using var result = await client.GetObjectAsync(bucket, key, ct);
            var bytes = new byte[32]; var read = await result.ResponseStream.ReadAsync(bytes, ct);
            return HasMatchingImageSignature(bytes.AsSpan(0, read), contentType);
        }
        catch (AmazonS3Exception) { metrics.RecordR2Failure(); return false; }
    }
    public async Task<bool> DeleteAsync(string key, CancellationToken ct)
    {
        if (client is null || bucket is null) return false;
        try { await client.DeleteObjectAsync(bucket, key, ct); return true; }
        catch (AmazonS3Exception) { metrics.RecordR2Failure(); return false; }
    }
    public async Task<byte[]?> DownloadAsync(string key, CancellationToken ct) { if (client is null || bucket is null) return null; try { using var result = await client.GetObjectAsync(bucket, key, ct); using var stream = new MemoryStream(); await result.ResponseStream.CopyToAsync(stream, ct); return stream.ToArray(); } catch (AmazonS3Exception) { metrics.RecordR2Failure(); return null; } }
    public async Task<bool> UploadAsync(string key, string contentType, byte[] content, CancellationToken ct) { if (client is null || bucket is null) return false; try { using var input = new MemoryStream(content); await client.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = key, InputStream = input, ContentType = contentType }, ct); return true; } catch (AmazonS3Exception) { metrics.RecordR2Failure(); return false; } }

    private static bool HasMatchingImageSignature(ReadOnlySpan<byte> bytes, string contentType) => contentType switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
        "image/png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
        "image/webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
        "image/heic" => bytes.Length >= 12 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8) && (bytes.Slice(8, 4).SequenceEqual("heic"u8) || bytes.Slice(8, 4).SequenceEqual("heix"u8) || bytes.Slice(8, 4).SequenceEqual("mif1"u8)),
        _ => false
    };
}
