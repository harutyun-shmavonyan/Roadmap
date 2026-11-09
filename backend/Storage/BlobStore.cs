using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Roadmap.Api.Storage;

/// <summary>One object in the bucket, as a listing reports it.</summary>
public sealed record BlobObject(string Key, long Size, DateTime LastModifiedUtc);

/// <summary>
/// Where file bytes live: the Railway bucket (S3-compatible) since 2026-11-09. Before that every
/// image and tailored CV was a bytea column, which put ~270 MB of a 308 MB database into four
/// tables on a 500 MB volume. Keys stored in the database are full keys, prefix included, so a
/// change of <see cref="Prefix"/> never orphans what is already written.
/// </summary>
public interface IBlobStore
{
    /// <summary>False when no bucket is configured: writes then stay in Postgres, as before.</summary>
    bool Enabled { get; }

    /// <summary>Every key this app writes starts with this (e.g. "roadmap/"), so a shared bucket stays tidy.</summary>
    string Prefix { get; }

    Task PutAsync(string key, byte[] data, string contentType, CancellationToken ct = default);

    /// <summary>The object's bytes, or null when there is no such key.</summary>
    Task<byte[]?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>The object's size, or null when there is no such key.</summary>
    Task<long?> SizeAsync(string key, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>Every object under <see cref="Prefix"/>.</summary>
    IAsyncEnumerable<BlobObject> ListAsync(CancellationToken ct = default);
}

/// <summary>
/// Bucket settings from the service's variables. On Railway they are references to the bucket's
/// own credentials (<c>BLOB_BUCKET=${{embedded-tin.BUCKET}}</c> and so on), so rotating the
/// bucket's keys needs no change here. Prefixed names because ACCESS_KEY_ID and friends are too
/// generic to claim for one purpose.
/// </summary>
public sealed record BlobSettings(
    string? Bucket, string? Endpoint, string Region, string? AccessKeyId, string? SecretAccessKey,
    bool ForcePathStyle, string Prefix, bool Backfill, bool Sweep, TimeSpan SweepGrace)
{
    public bool Complete =>
        !string.IsNullOrWhiteSpace(Bucket) && !string.IsNullOrWhiteSpace(AccessKeyId)
        && !string.IsNullOrWhiteSpace(SecretAccessKey);

    public static BlobSettings FromEnvironment()
    {
        static string? Env(string k) => Environment.GetEnvironmentVariable(k) is { Length: > 0 } v ? v.Trim() : null;
        static bool Flag(string k, bool dflt) => Env(k) is string v
            ? v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1"
            : dflt;
        var prefix = Env("BLOB_PREFIX") ?? "roadmap/";
        if (!prefix.EndsWith('/')) prefix += "/";
        return new BlobSettings(
            Env("BLOB_BUCKET"), Env("BLOB_ENDPOINT"), Env("BLOB_REGION") ?? "auto",
            Env("BLOB_ACCESS_KEY_ID"), Env("BLOB_SECRET_ACCESS_KEY"),
            Flag("BLOB_FORCE_PATH_STYLE", false), prefix,
            Flag("BLOB_BACKFILL", true), Flag("BLOB_SWEEP", true),
            // How old an unreferenced object must be before the sweeper deletes it. An hour by
            // default, so an upload whose row is still being saved is never touched; tests set 0.
            TimeSpan.FromMinutes(int.TryParse(Env("BLOB_SWEEP_GRACE_MINUTES"), out var m) && m >= 0 ? m : 60));
    }
}

/// <summary>No bucket configured. Every caller checks <see cref="Enabled"/> before writing.</summary>
public sealed class DisabledBlobStore(string prefix) : IBlobStore
{
    public bool Enabled => false;
    public string Prefix => prefix;
    static InvalidOperationException Off() => new("No bucket is configured (BLOB_BUCKET and its keys are unset).");
    public Task PutAsync(string key, byte[] data, string contentType, CancellationToken ct = default) => throw Off();
    public Task<byte[]?> GetAsync(string key, CancellationToken ct = default) => throw Off();
    public Task<long?> SizeAsync(string key, CancellationToken ct = default) => throw Off();
    public Task DeleteAsync(string key, CancellationToken ct = default) => throw Off();
    public async IAsyncEnumerable<BlobObject> ListAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

public sealed class S3BlobStore : IBlobStore, IDisposable
{
    private readonly AmazonS3Client _s3;
    private readonly string _bucket;

    public S3BlobStore(BlobSettings s)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = s.ForcePathStyle,
            AuthenticationRegion = s.Region,
            // S3-compatible stores do not all accept the SDK's default flexible checksums.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        if (!string.IsNullOrWhiteSpace(s.Endpoint)) config.ServiceURL = s.Endpoint;
        else config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(s.Region);
        _s3 = new AmazonS3Client(new BasicAWSCredentials(s.AccessKeyId, s.SecretAccessKey), config);
        _bucket = s.Bucket!;
        Prefix = s.Prefix;
    }

    public bool Enabled => true;
    public string Prefix { get; }

    public async Task PutAsync(string key, byte[] data, string contentType, CancellationToken ct = default)
    {
        using var body = new MemoryStream(data, writable: false);
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket, Key = key, InputStream = body,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            AutoCloseStream = false, UseChunkEncoding = false,
        }, ct);
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _s3.GetObjectAsync(_bucket, key, ct);
            using var ms = new MemoryStream(resp.ContentLength > 0 ? (int)resp.ContentLength : 0);
            await resp.ResponseStream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound) { return null; }
    }

    public async Task<long?> SizeAsync(string key, CancellationToken ct = default)
    {
        try { return (await _s3.GetObjectMetadataAsync(_bucket, key, ct)).ContentLength; }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound) { return null; }
    }

    public Task DeleteAsync(string key, CancellationToken ct = default) =>
        _s3.DeleteObjectAsync(_bucket, key, ct);

    public async IAsyncEnumerable<BlobObject> ListAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var req = new ListObjectsV2Request { BucketName = _bucket, Prefix = Prefix };
        while (true)
        {
            var resp = await _s3.ListObjectsV2Async(req, ct);
            foreach (var o in resp.S3Objects ?? [])
                yield return new BlobObject(o.Key, o.Size, o.LastModified.ToUniversalTime());
            if (resp.IsTruncated != true || string.IsNullOrEmpty(resp.NextContinuationToken)) yield break;
            req.ContinuationToken = resp.NextContinuationToken;
        }
    }

    public void Dispose() => _s3.Dispose();
}
