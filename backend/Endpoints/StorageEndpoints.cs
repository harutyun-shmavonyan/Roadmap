using Roadmap.Api.Data;
using Roadmap.Api.Storage;

namespace Roadmap.Api.Endpoints;

/// <summary>REST twin of the storage MCP tools: status (the proof) and a manual maintenance run.</summary>
public static class StorageEndpoints
{
    public static void MapStorageEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/storage").WithTags("Storage").RequireAuthorization();

        g.MapGet("/status", async (bool? deep, RoadmapDbContext db, IBlobStore store) =>
            Results.Ok(await BlobMaintenance.StatusAsync(db, store, deep == true)));

        g.MapPost("/maintenance", async (bool? sweep, RoadmapDbContext db, IBlobStore store, BlobSettings settings, ILogger<IBlobStore> log) =>
        {
            if (!store.Enabled) return Results.BadRequest("No bucket is configured (BLOB_BUCKET and its keys are unset).");
            var moved = await BlobMaintenance.BackfillAsync(db, store, log);
            var swept = sweep == false ? null : await BlobMaintenance.SweepAsync(db, store, settings.SweepGrace, log);
            return Results.Ok(new { backfill = moved, sweep = swept });
        });
    }
}
