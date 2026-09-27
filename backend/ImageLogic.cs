using Roadmap.Api.Data;

namespace Roadmap.Api;

/// <summary>
/// The parts of taking an image in that do not care what the image is for: deciding whether bytes
/// are an image at all, decoding one out of a tool call, and fetching one from a URL. Meals were
/// the first to need this and grew their own copy; experiences are the second, so it lives here
/// now and <see cref="MealLogic"/> forwards to it, rather than there being two slightly different
/// answers to "is this a picture".
/// </summary>
public static class ImageLogic
{
    /// <summary>Generous for a phone photo, far under Kestrel's request cap.</summary>
    public const long MaxImageBytes = 20L * 1024 * 1024;

    public const string MaxImageHelp = "20 MB";

    /// <summary>
    /// Settle on a MIME type for an upload: trust a declared image/* type, else read it off the
    /// bytes, else fall back to the filename's extension. Null when none of the three says
    /// "image", which is the signal to reject — these are rendered in an &lt;img&gt;.
    /// </summary>
    public static string? ResolveContentType(string? declared, string? fileName, byte[]? bytes = null)
    {
        var ct = declared?.Trim();
        if (IsImageType(ct)) return ct!.ToLowerInvariant();
        // The bytes are more trustworthy than a name: an assistant often has neither a type nor a
        // filename to offer, and a phone upload often declares application/octet-stream.
        if (bytes is not null && SniffType(bytes) is string sniffed) return sniffed;
        var guessed = string.IsNullOrWhiteSpace(fileName) ? null : ArticleLogic.GuessContentType(fileName!);
        return IsImageType(guessed) ? guessed : null;
    }

    /// <summary>The image type the bytes themselves declare, by magic number, or null.</summary>
    public static string? SniffType(byte[] b)
    {
        static bool Ascii(byte[] b, int at, string tag) =>
            b.Length >= at + tag.Length && !tag.Where((c, i) => b[at + i] != (byte)c).Any();

        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return "image/png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (Ascii(b, 0, "GIF8")) return "image/gif";
        if (Ascii(b, 0, "RIFF") && Ascii(b, 8, "WEBP")) return "image/webp";
        if (Ascii(b, 4, "ftyp") && (Ascii(b, 8, "avif") || Ascii(b, 8, "avis"))) return "image/avif";
        if (Ascii(b, 0, "BM")) return "image/bmp";
        return null;
    }

    public static bool IsImageType(string? ct) =>
        !string.IsNullOrWhiteSpace(ct) && ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Decode an image handed over as base64 — bare, or as a full <c>data:image/...;base64,</c>
    /// URI, in which case the type in the header is returned too. Returns an error message
    /// instead of throwing, because every caller turns a bad upload into a reply, not a 500.
    /// </summary>
    public static (byte[]? Bytes, string? DeclaredType, string? Error) DecodeBase64(string? input)
    {
        var b64 = input?.Trim() ?? "";
        string? declared = null;
        if (b64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = b64.IndexOf(',');
            if (comma < 0) return (null, null, "malformed data URI");
            var header = b64[5..comma]; // e.g. "image/png;base64"
            if (header.Length > 0) declared = header.Split(';')[0];
            b64 = b64[(comma + 1)..];
        }
        byte[] bytes;
        try { bytes = Convert.FromBase64String(b64); }
        catch (FormatException) { return (null, null, "data_base64 is not valid base64"); }
        if (bytes.Length == 0) return (null, null, "image data is empty");
        if (bytes.Length > MaxImageBytes) return (null, null, $"image exceeds {MaxImageHelp}");
        return (bytes, declared, null);
    }

    /// <summary>
    /// Fetch an image by URL on the server's side, so the bytes never pass through a tool call.
    /// Private and loopback hosts are refused; the size cap is enforced while streaming, so an
    /// endless response cannot fill memory before it is noticed.
    /// </summary>
    public static async Task<(byte[]? Bytes, string? HeaderType, string? FileName, string? Error)> DownloadAsync(
        IHttpClientFactory httpFactory, string? url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return (null, null, null, "url must be an absolute http(s) URL");
        if (await UrlGuard.IsBlockedHostAsync(uri))
            return (null, null, null, "url host is not allowed (private/loopback addresses are blocked)");

        try
        {
            var http = httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("RoadmapBot/1.0");
            using var resp = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode) return (null, null, null, $"fetch failed: HTTP {(int)resp.StatusCode}");
            var headerType = resp.Content.Headers.ContentType?.MediaType;

            await using var src = await resp.Content.ReadAsStreamAsync();
            using var ms = new MemoryStream();
            var buf = new byte[81920];
            int read;
            while ((read = await src.ReadAsync(buf)) > 0)
            {
                if (ms.Length + read > MaxImageBytes) return (null, null, null, $"image exceeds {MaxImageHelp}");
                ms.Write(buf, 0, read);
            }
            if (ms.Length == 0) return (null, null, null, "downloaded image is empty");
            return (ms.ToArray(), headerType, Path.GetFileName(uri.LocalPath), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (null, null, null, $"could not download the image: {ex.Message}");
        }
    }

    /// <summary>A filename safe to keep, or null — only ever used to name a re-download.</summary>
    public static string? CleanFileName(string? fileName)
    {
        var clean = string.IsNullOrWhiteSpace(fileName) ? "" : ArticleLogic.SanitizeImageName(fileName);
        return clean.Length == 0 ? null : clean;
    }
}
