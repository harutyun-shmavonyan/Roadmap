namespace Roadmap.Api;

/// <summary>
/// Single source of truth for "today".
///
/// Everything user-facing in this app is anchored to Armenia (Asia/Yerevan), which is UTC+4
/// year-round — no DST since 2012. The container runs on UTC, so <c>DateTime.Today</c> and
/// <c>DateTime.UtcNow</c> both report the *previous* day between 00:00 and 04:00 local time.
/// That off-by-one matters: it decides which day is "past" (frozen) and which is "today"
/// (still re-plannable) when a sprint plan is rebuilt.
/// </summary>
public static class AppClock
{
    // Resolve via the tz database when available, but fall back to a fixed offset since the
    // alpine runtime image ships without tzdata and InvariantGlobalization is enabled.
    private static readonly TimeZoneInfo Tz = Resolve();

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "Asia/Yerevan", "Caucasus Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        return TimeZoneInfo.CreateCustomTimeZone("Yerevan+4", TimeSpan.FromHours(4), "Yerevan", "Yerevan");
    }

    public static DateTime Now() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz);

    public static DateOnly Today() => DateOnly.FromDateTime(Now());

    /// <summary>
    /// The UTC instant a local calendar day begins — for comparing stored UTC timestamps
    /// against a <see cref="DateOnly"/> boundary such as a sprint's first morning.
    /// </summary>
    public static DateTime StartOfDayUtc(DateOnly localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(TimeOnly.MinValue), Tz);

    /// <summary>
    /// Local calendar date of a stored UTC timestamp (e.g. <c>StatusChange.ChangedAt</c>).
    /// </summary>
    public static DateOnly ToLocalDate(DateTime utc)
    {
        var asUtc = utc.Kind == DateTimeKind.Local
            ? utc.ToUniversalTime()
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(asUtc, Tz));
    }
}
