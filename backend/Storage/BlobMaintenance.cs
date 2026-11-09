using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;

namespace Roadmap.Api.Storage;

/// <summary>
/// Moving files into the bucket and keeping it tidy. Three jobs, all safe to run any number of times:
/// <list type="bullet">
/// <item><b>Backfill</b> copies every file still only in Postgres into the bucket and records its
/// key. The bytea is deliberately left in place: dropping it is a separate cleanup migration, run
/// when the user decides (their call, 2026-11-09), once <see cref="StatusAsync"/> shows nothing
/// depends on it.</item>
/// <item><b>Sweep</b> deletes bucket objects no row references any more — a replaced image, a
/// deleted article, experience or meal (their images cascade away in the database), a job-run day
/// replaced by a re-import. One mechanism instead of a hook on every delete path. Objects younger
/// than the grace period are never touched, so an upload whose row is still being saved
/// is safe (BLOB_SWEEP_GRACE_MINUTES, an hour by default).</item>
/// <item><b>Status</b> is the proof: per table, how many files are in the bucket, how many still
/// only in Postgres, and whether every referenced object really exists (and, with deep, holds the
/// same bytes as the Postgres copy it was made from).</item>
/// </list>
/// </summary>
public static class BlobMaintenance
{
    private sealed record Pending(Guid Id, Guid OwnerId, string ContentType, byte[] Bytes);

    // ── backfill ──

    public sealed record BackfillResult(string Table, int Moved, long Bytes, string? Error);

    public static async Task<List<BackfillResult>> BackfillAsync(RoadmapDbContext db, IBlobStore store,
        ILogger? log = null, CancellationToken ct = default)
    {
        var results = new List<BackfillResult>();
        if (!store.Enabled) return results;

        results.Add(await MoveAsync("article_images", log, ct,
            take => db.ArticleImages.AsNoTracking()
                .Where(i => i.StorageKey == null && i.Data.Length > 0).OrderBy(i => i.Id).Take(take)
                .Select(i => new Pending(i.Id, i.ArticleId, i.ContentType, i.Data)).ToListAsync(ct),
            p => BlobFiles.ArticleImageKey(store, p.OwnerId, p.Id),
            (p, key, size) => db.ArticleImages.Where(i => i.Id == p.Id && i.StorageKey == null)
                .ExecuteUpdateAsync(u => u.SetProperty(i => i.StorageKey, key).SetProperty(i => i.SizeBytes, size), ct),
            store));

        results.Add(await MoveAsync("experience_images", log, ct,
            take => db.ExperienceImages.AsNoTracking()
                .Where(i => i.StorageKey == null && i.Data.Length > 0).OrderBy(i => i.Id).Take(take)
                .Select(i => new Pending(i.Id, i.ExperienceId, i.ContentType, i.Data)).ToListAsync(ct),
            p => BlobFiles.ExperienceImageKey(store, p.OwnerId, p.Id),
            (p, key, size) => db.ExperienceImages.Where(i => i.Id == p.Id && i.StorageKey == null)
                .ExecuteUpdateAsync(u => u.SetProperty(i => i.StorageKey, key).SetProperty(i => i.SizeBytes, size), ct),
            store));

        results.Add(await MoveAsync("meal_images", log, ct,
            take => db.MealImages.AsNoTracking()
                .Where(i => i.StorageKey == null && i.Data.Length > 0).OrderBy(i => i.MealId).Take(take)
                .Select(i => new Pending(i.MealId, i.MealId, i.ContentType, i.Data)).ToListAsync(ct),
            p => BlobFiles.MealImageKey(store, p.OwnerId),
            (p, key, size) => db.MealImages.Where(i => i.MealId == p.Id && i.StorageKey == null)
                .ExecuteUpdateAsync(u => u.SetProperty(i => i.StorageKey, key).SetProperty(i => i.SizeBytes, size), ct),
            store));

        results.Add(await MoveAsync("job_postings", log, ct,
            take => db.JobPostings.AsNoTracking()
                .Where(p => p.CvStorageKey == null && p.TailoredCvPdf != null && p.TailoredCvPdf.Length > 0)
                .OrderBy(p => p.Id).Take(take)
                .Select(p => new Pending(p.Id, p.Id, "application/pdf", p.TailoredCvPdf!)).ToListAsync(ct),
            p => BlobFiles.JobCvKey(store, p.Id),
            (p, key, size) => db.JobPostings.Where(x => x.Id == p.Id && x.CvStorageKey == null)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.CvStorageKey, key).SetProperty(x => x.CvSizeBytes, size), ct),
            store));

        return results;
    }

    private static async Task<BackfillResult> MoveAsync(string table, ILogger? log, CancellationToken ct,
        Func<int, Task<List<Pending>>> nextBatch, Func<Pending, string> keyFor,
        Func<Pending, string, long, Task<int>> record, IBlobStore store)
    {
        int moved = 0; long bytes = 0;
        try
        {
            // Small batches: each row can be megabytes, and the service has little memory.
            while (!ct.IsCancellationRequested)
            {
                var batch = await nextBatch(10);
                if (batch.Count == 0) break;
                foreach (var p in batch)
                {
                    var key = keyFor(p);
                    await store.PutAsync(key, p.Bytes, p.ContentType, ct);
                    // Read it back before pointing the row at it: a key is only recorded for an
                    // object that holds exactly these bytes.
                    var back = await store.GetAsync(key, ct);
                    if (back is null || !back.AsSpan().SequenceEqual(p.Bytes))
                        throw new InvalidOperationException($"Read-back of {key} did not match the Postgres copy.");
                    // 0 rows = the row changed or vanished meanwhile; the object is then
                    // unreferenced and the sweeper takes it.
                    if (await record(p, key, p.Bytes.LongLength) == 1) { moved++; bytes += p.Bytes.LongLength; }
                }
            }
            if (moved > 0) log?.LogInformation("Blob backfill: moved {Count} file(s), {Bytes} bytes, from {Table}", moved, bytes, table);
            return new BackfillResult(table, moved, bytes, null);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log?.LogError(e, "Blob backfill of {Table} stopped after {Count} file(s)", table, moved);
            return new BackfillResult(table, moved, bytes, e.Message);
        }
    }

    // ── sweep ──

    public sealed record SweepResult(int Objects, int Referenced, int Deleted, long DeletedBytes, string? Skipped);

    public static async Task<HashSet<string>> ReferencedKeysAsync(RoadmapDbContext db, CancellationToken ct = default)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        keys.UnionWith(await db.ArticleImages.Where(i => i.StorageKey != null).Select(i => i.StorageKey!).ToListAsync(ct));
        keys.UnionWith(await db.ExperienceImages.Where(i => i.StorageKey != null).Select(i => i.StorageKey!).ToListAsync(ct));
        keys.UnionWith(await db.MealImages.Where(i => i.StorageKey != null).Select(i => i.StorageKey!).ToListAsync(ct));
        keys.UnionWith(await db.JobPostings.Where(p => p.CvStorageKey != null).Select(p => p.CvStorageKey!).ToListAsync(ct));
        return keys;
    }

    public static async Task<SweepResult> SweepAsync(RoadmapDbContext db, IBlobStore store, TimeSpan grace,
        ILogger? log = null, CancellationToken ct = default)
    {
        if (!store.Enabled) return new SweepResult(0, 0, 0, 0, "no bucket configured");
        // The listing is taken first: anything uploaded after it is not considered at all.
        var objects = new List<BlobObject>();
        await foreach (var o in store.ListAsync(ct)) objects.Add(o);
        var referenced = await ReferencedKeysAsync(db, ct);
        // A database that references nothing while the bucket holds files is far more likely to be
        // the wrong database (or a failed read) than one where every file was deleted. Do nothing.
        if (referenced.Count == 0 && objects.Count > 0)
            return new SweepResult(objects.Count, 0, 0, 0, "no row references any object; refusing to empty the bucket");

        var cutoff = DateTime.UtcNow - grace;
        int deleted = 0; long deletedBytes = 0;
        foreach (var o in objects.Where(o => !referenced.Contains(o.Key) && o.LastModifiedUtc < cutoff))
        {
            await store.DeleteAsync(o.Key, ct);
            deleted++; deletedBytes += o.Size;
        }
        if (deleted > 0) log?.LogInformation("Blob sweep: deleted {Count} unreferenced object(s), {Bytes} bytes", deleted, deletedBytes);
        return new SweepResult(objects.Count, referenced.Count, deleted, deletedBytes, null);
    }

    // ── status ──

    public sealed record TableStatus(
        string Table,
        int Files,               // rows that have a file at all
        int InBucket,            // ... whose file is in the bucket (StorageKey set)
        int PostgresOnly,        // ... whose file exists only as bytea — these still depend on Postgres
        long BucketBytes,        // sum of SizeBytes for the files in the bucket
        int MissingInBucket,     // StorageKey set but no such object (or a size mismatch)
        int LegacyCopies,        // bucket files whose old bytea copy is still in Postgres (cleanup drops them)
        long LegacyBytes,
        int? Verified,           // deep only: legacy copies compared byte for byte with the bucket
        int? Mismatched);        // deep only: ... and found different

    public sealed record StorageStatus(
        bool BucketConfigured, string Prefix, bool ReliesOnPostgres, bool ReadyForCleanup,
        List<TableStatus> Tables, int BucketObjects, long BucketObjectBytes, int UnreferencedObjects,
        string Summary);

    private sealed record Row(string? Key, long Size, int InlineLength);

    public static async Task<StorageStatus> StatusAsync(RoadmapDbContext db, IBlobStore store, bool deep,
        CancellationToken ct = default)
    {
        Dictionary<string, BlobObject> objects = new(StringComparer.Ordinal);
        if (store.Enabled)
            await foreach (var o in store.ListAsync(ct)) objects[o.Key] = o;

        var tables = new List<TableStatus>
        {
            await TableAsync("article_images",
                db.ArticleImages.AsNoTracking().Select(i => new Row(i.StorageKey, i.SizeBytes, i.Data.Length)),
                deep ? (k => db.ArticleImages.AsNoTracking().Where(i => i.StorageKey == k).Select(i => i.Data).FirstAsync(ct)) : null),
            await TableAsync("experience_images",
                db.ExperienceImages.AsNoTracking().Select(i => new Row(i.StorageKey, i.SizeBytes, i.Data.Length)),
                deep ? (k => db.ExperienceImages.AsNoTracking().Where(i => i.StorageKey == k).Select(i => i.Data).FirstAsync(ct)) : null),
            await TableAsync("meal_images",
                db.MealImages.AsNoTracking().Select(i => new Row(i.StorageKey, i.SizeBytes, i.Data.Length)),
                deep ? (k => db.MealImages.AsNoTracking().Where(i => i.StorageKey == k).Select(i => i.Data).FirstAsync(ct)) : null),
            await TableAsync("job_postings (tailored CVs)",
                db.JobPostings.AsNoTracking().Select(p => new Row(p.CvStorageKey, p.CvSizeBytes,
                    p.TailoredCvPdf == null ? 0 : p.TailoredCvPdf.Length)),
                deep ? (k => db.JobPostings.AsNoTracking().Where(p => p.CvStorageKey == k).Select(p => p.TailoredCvPdf!).FirstAsync(ct)) : null),
        };

        async Task<TableStatus> TableAsync(string name, IQueryable<Row> q, Func<string, Task<byte[]>>? legacyBytes)
        {
            var rows = await q.ToListAsync(ct);
            var withFile = rows.Where(r => r.Key != null || r.InlineLength > 0).ToList();
            var inBucket = withFile.Where(r => r.Key != null).ToList();
            var missing = inBucket.Count(r => !objects.TryGetValue(r.Key!, out var o) || o.Size != r.Size);
            var legacy = inBucket.Where(r => r.InlineLength > 0).ToList();
            int? verified = null, mismatched = null;
            if (legacyBytes is not null && store.Enabled)
            {
                verified = 0; mismatched = 0;
                // One file at a time: these can be megabytes each.
                foreach (var r in legacy)
                {
                    var a = await legacyBytes(r.Key!);
                    var b = await store.GetAsync(r.Key!, ct);
                    verified++;
                    if (b is null || !SHA256.HashData(a).AsSpan().SequenceEqual(SHA256.HashData(b))) mismatched++;
                }
            }
            return new TableStatus(name, withFile.Count, inBucket.Count, withFile.Count - inBucket.Count,
                inBucket.Sum(r => r.Size), missing, legacy.Count, legacy.Sum(r => (long)r.InlineLength),
                verified, mismatched);
        }

        var referenced = store.Enabled ? await ReferencedKeysAsync(db, ct) : [];
        var unreferenced = objects.Keys.Count(k => !referenced.Contains(k));
        var relies = tables.Any(t => t.PostgresOnly > 0);
        var broken = tables.Any(t => t.MissingInBucket > 0 || t.Mismatched > 0);
        var ready = store.Enabled && !relies && !broken;
        var summary = !store.Enabled ? "No bucket configured: every file is still read from and written to Postgres."
            : broken ? "Some rows point at bucket objects that are missing or differ — do NOT clean up."
            : relies ? $"{tables.Sum(t => t.PostgresOnly)} file(s) still live only in Postgres; the backfill has not finished."
            : $"Every file is served from the bucket. {tables.Sum(t => t.LegacyCopies)} legacy Postgres copies " +
              $"({tables.Sum(t => t.LegacyBytes) / 1048576.0:F1} MB) are no longer read and can be dropped by the cleanup migration.";
        return new StorageStatus(store.Enabled, store.Prefix, relies, ready, tables,
            objects.Count, objects.Values.Sum(o => o.Size), unreferenced, summary);
    }
}

/// <summary>
/// Runs the backfill once shortly after start, then the backfill and the sweep once a day. A
/// bucket outage only logs: requests keep working, because nothing here is on a request's path.
/// </summary>
public sealed class BlobMaintenanceService(IServiceScopeFactory scopes, IBlobStore store, BlobSettings settings,
    ILogger<BlobMaintenanceService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (!store.Enabled) { log.LogInformation("Blob storage: no bucket configured; files stay in Postgres."); return; }
        log.LogInformation("Blob storage: bucket configured, prefix {Prefix}", store.Prefix);
        try { await Task.Delay(TimeSpan.FromSeconds(20), stop); } catch (OperationCanceledException) { return; }
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<RoadmapDbContext>();
                if (settings.Backfill) await BlobMaintenance.BackfillAsync(db, store, log, stop);
                if (settings.Sweep) await BlobMaintenance.SweepAsync(db, store, settings.SweepGrace, log, stop);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e) { log.LogError(e, "Blob maintenance pass failed"); }
            try { await Task.Delay(TimeSpan.FromHours(24), stop); } catch (OperationCanceledException) { return; }
        }
    }
}
