using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Roadmap.Api.Data;
using Roadmap.Api.Storage;

namespace Roadmap.Api.Mcp;

/// <summary>
/// Where the app's files live (the bucket since 2026-11-09) and the proof that nothing still
/// depends on the old bytea columns. Read-mostly: the two write tools only copy files into the
/// bucket or delete objects no row references.
/// </summary>
[McpServerToolType]
public sealed class StorageMcpTools(RoadmapDbContext db, IBlobStore store, BlobSettings settings, ILogger<StorageMcpTools> log)
{
    private static string J(object? v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true });

    [McpServerTool(Name = "storage_status"), Description(
        "Report where the app's files (article images, experience photos, meal photos, tailored CV PDFs) are stored. " +
        "Per table: files in the bucket, files still only in Postgres (postgres_only > 0 means the app still depends on " +
        "Postgres for them), bucket objects that are missing, and legacy Postgres copies that the cleanup migration " +
        "would drop. ready_for_cleanup is true only when every file is served from the bucket and every referenced " +
        "object exists. deep=true also downloads every bucket file that still has a Postgres copy and compares the two " +
        "byte for byte — slower (it reads every file once), use it once before cleanup.")]
    public async Task<string> StorageStatus(
        [Description("Compare every bucket file with its legacy Postgres copy byte for byte. Default false.")] bool deep = false)
        => J(await BlobMaintenance.StatusAsync(db, store, deep));

    [McpServerTool(Name = "run_storage_maintenance"), Description(
        "Run the storage maintenance now instead of waiting for the daily pass: copy any file still only in Postgres " +
        "into the bucket (the Postgres copy is kept until the cleanup migration), then delete bucket objects that no " +
        "row references and that are older than an hour. Safe to run any number of times.")]
    public async Task<string> RunStorageMaintenance(
        [Description("Also delete unreferenced bucket objects. Default true.")] bool sweep = true)
    {
        if (!store.Enabled) return J(new { error = "No bucket is configured (BLOB_BUCKET and its keys are unset)." });
        var moved = await BlobMaintenance.BackfillAsync(db, store, log);
        var swept = sweep ? await BlobMaintenance.SweepAsync(db, store, settings.SweepGrace, log) : null;
        return J(new { backfill = moved, sweep = swept });
    }
}
