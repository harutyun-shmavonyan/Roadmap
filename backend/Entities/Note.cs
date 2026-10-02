namespace Roadmap.Api.Entities;

/// <summary>
/// A daily note for one of the two books ('red' | 'green'). At most one row per
/// (book, entry_date); same-day additions append to the existing entry's content.
/// DayNumber is a per-book sequential counter (1, 2, 3, …) assigned on first entry of a day.
/// </summary>
public class Note
{
    public Guid Id { get; set; }

    /// <summary>Which book this note belongs to: 'red' or 'green'.</summary>
    public string Book { get; set; } = string.Empty;

    /// <summary>Per-book sequential day counter, starting at 1.</summary>
    public int DayNumber { get; set; }

    /// <summary>The calendar date this entry is for (Asia/Yerevan).</summary>
    public DateOnly EntryDate { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
