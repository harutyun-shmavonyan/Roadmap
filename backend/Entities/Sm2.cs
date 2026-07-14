namespace Roadmap.Api.Entities;

/// <summary>
/// The SM-2 spaced-repetition scheduler (SuperMemo 2, Wozniak 1987).
///
/// Deliberately a pure function over the entry's state: scheduling must be deterministic
/// and reproducible, so it lives here on the server rather than in whatever prompt or
/// skill happens to be driving the review. The caller supplies only a grade.
/// </summary>
public static class Sm2
{
    /// <summary>Below this, the answer counts as a failure and the interval collapses.</summary>
    public const int PassThreshold = 3;

    public const double MinEase = 1.3;
    public const double StartingEase = 2.5;

    public readonly record struct State(int Repetitions, double EaseFactor, int IntervalDays);

    /// <summary>
    /// Apply a review of quality <paramref name="grade"/> (0–5) to <paramref name="current"/>
    /// and return the next state.
    ///
    /// A pass (grade >= 3) advances the interval: 1 day, then 6 days, then interval * ease.
    /// A failure (grade &lt; 3) resets repetitions and sends the item back to a 1-day interval —
    /// but the ease factor is retained (reduced), so a chronically hard item keeps its
    /// hard-earned low ease instead of pretending to be new.
    /// </summary>
    public static State Next(State current, int grade)
    {
        grade = Math.Clamp(grade, 0, 5);

        // The ease adjustment is applied on every review, pass or fail (SM-2 §EF').
        var ease = current.EaseFactor + (0.1 - (5 - grade) * (0.08 + (5 - grade) * 0.02));
        if (ease < MinEase) ease = MinEase;

        if (grade < PassThreshold)
        {
            return new State(Repetitions: 0, EaseFactor: ease, IntervalDays: 1);
        }

        var repetitions = current.Repetitions + 1;
        var interval = repetitions switch
        {
            1 => 1,
            2 => 6,
            _ => (int)Math.Round(current.IntervalDays * ease, MidpointRounding.AwayFromZero),
        };

        // Guard the degenerate case where a stored interval of 0 would keep the item pinned at 0.
        if (interval < 1) interval = 1;

        return new State(repetitions, ease, interval);
    }
}
