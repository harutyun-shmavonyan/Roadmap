namespace Roadmap.Api.Storage;

/// <summary>
/// A row that owns one file: its bytes are either in the bucket (<see cref="StorageKey"/> set) or,
/// for rows written before the bucket existed or while none is configured, still inline in
/// Postgres (<see cref="InlineBytes"/>). Implemented explicitly by the four entities that hold
/// files, so the column names stay what they are and EF maps nothing new from here.
/// </summary>
public interface IBlobHolder
{
    string? StorageKey { get; set; }
    long SizeBytes { get; set; }
    byte[]? InlineBytes { get; set; }
}

/// <summary>
/// The one place that decides where a file's bytes go and where they are read from. Every write
/// site (REST, MCP, import) and every read site (downloads, the article HTML builder) goes
/// through here, so "bucket first, Postgres only as the fallback" cannot drift between them.
/// </summary>
public static class BlobFiles
{
    // Key layout, under the store's prefix. The random suffix makes every write a new object, so a
    // replace never overwrites bytes a reader is mid-way through; the old object becomes
    // unreferenced and the sweeper removes it (see BlobMaintenance).
    public static string ArticleImageKey(IBlobStore s, Guid articleId, Guid imageId) =>
        $"{s.Prefix}articles/{articleId}/{imageId}-{Suffix()}";
    public static string ExperienceImageKey(IBlobStore s, Guid experienceId, Guid imageId) =>
        $"{s.Prefix}experiences/{experienceId}/{imageId}-{Suffix()}";
    public static string MealImageKey(IBlobStore s, Guid mealId) =>
        $"{s.Prefix}meals/{mealId}-{Suffix()}";
    public static string JobCvKey(IBlobStore s, Guid postingId) =>
        $"{s.Prefix}job-cvs/{postingId}-{Suffix()}.pdf";

    private static string Suffix() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>
    /// Store a file for <paramref name="holder"/>: in the bucket when one is configured (and the
    /// Postgres column is emptied, so a new write never adds bytes to the database), else inline
    /// as before. Uploads before the caller saves, so a failed upload leaves the row untouched; a
    /// failed save leaves an unreferenced object, which the sweeper removes.
    /// </summary>
    public static async Task StoreAsync(IBlobStore store, IBlobHolder holder, string key,
        byte[] bytes, string contentType, CancellationToken ct = default)
    {
        if (store.Enabled)
        {
            await store.PutAsync(key, bytes, contentType, ct);
            holder.StorageKey = key;
            holder.InlineBytes = null;
        }
        else
        {
            holder.StorageKey = null;
            holder.InlineBytes = bytes;
        }
        holder.SizeBytes = bytes.LongLength;
    }

    /// <summary>
    /// The bytes for a file the caller already resolved to either a key or inline bytes — the
    /// query projections pick the inline column only when the key is null, so a row already in
    /// the bucket never drags its old bytea out of Postgres. Null when there is no file, or the
    /// key names an object the bucket no longer has.
    /// </summary>
    public static async Task<byte[]?> LoadAsync(IBlobStore store, string? key, byte[]? inline,
        CancellationToken ct = default)
    {
        if (key is not null)
        {
            if (!store.Enabled)
                throw new InvalidOperationException($"File {key} is in the bucket, but no bucket is configured.");
            return await store.GetAsync(key, ct);
        }
        return inline is { Length: > 0 } ? inline : null;
    }
}
