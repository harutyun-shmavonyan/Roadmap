namespace Roadmap.Api.Entities;

/// <summary>How a review went. The numeric values are the FSRS rating scale.</summary>
public enum FsrsGrade
{
    /// <summary>Forgot it. The prompt comes back tomorrow and its stability collapses.</summary>
    Again = 1,
    /// <summary>Recalled, but wrong in part or with real effort. Stability grows a little.</summary>
    Hard = 2,
    /// <summary>Recalled correctly. The normal success.</summary>
    Good = 3,
    /// <summary>Recalled instantly and completely — or the learner said "O" (perfectly known). Stability jumps.</summary>
    Easy = 4,
}

/// <summary>
/// The FSRS-4.5 spaced-repetition scheduler (Free Spaced Repetition Scheduler, Jarrett Ye;
/// the DSR memory model: Difficulty, Stability, Retrievability), with its published default
/// parameters — fitted on hundreds of millions of real reviews and Anki's default since 2023.
///
/// Why FSRS and not SM-2 (which <see cref="Sm2"/> keeps for the vocabulary store): it models the
/// memory directly. <b>Stability</b> is the number of days until predicted recall falls to 90%;
/// every success multiplies it (so a prompt the learner keeps getting right vanishes for months,
/// then years) and every lapse cuts it (so a forgotten prompt returns within a day). There is no
/// "ease hell": stability recovers through later successes instead of being punished forever.
///
/// Like <see cref="Sm2"/> this is a pure function over state. It schedules at day granularity —
/// a review's elapsed time is whole calendar days (Asia/Yerevan) — because the notes are reviewed
/// in a daily session, not minute by minute.
/// </summary>
public static class Fsrs
{
    /// <summary>FSRS-4.5 default weights w0..w16.</summary>
    public static readonly double[] W =
    [
        0.4872, 1.4003, 3.7145, 13.8206, // w0-3  initial stability per grade (Again, Hard, Good, Easy)
        5.1618, 1.2298,                  // w4-5  initial difficulty
        0.8975, 0.031,                   // w6-7  difficulty step and mean reversion
        1.6474, 0.1367, 1.0461,          // w8-10 recall stability growth
        2.1072, 0.0793, 0.3246, 1.587,   // w11-14 post-lapse stability
        0.2272, 2.8755,                  // w15-16 hard penalty, easy bonus
    ];

    /// <summary>Power forgetting curve R(t) = (1 + Factor·t/S)^Decay, normalised so R(S) = 0.9.</summary>
    private const double Decay = -0.5;
    private static readonly double Factor = Math.Pow(0.9, 1 / Decay) - 1; // 19/81

    public const double MinDifficulty = 1, MaxDifficulty = 10;
    public const double MinStability = 0.1;
    public const int MaxIntervalDays = 3650;

    public readonly record struct State(double Difficulty, double Stability);

    /// <summary>The state right after a first exposure graded <paramref name="grade"/>.</summary>
    public static State Initial(FsrsGrade grade) =>
        new(InitialDifficulty(grade), Math.Max(W[(int)grade - 1], MinStability));

    /// <summary>Predicted probability of recall after <paramref name="elapsedDays"/> at this stability.</summary>
    public static double Retrievability(double stability, double elapsedDays)
    {
        if (elapsedDays <= 0) return 1;
        return Math.Pow(1 + Factor * elapsedDays / Math.Max(stability, MinStability), Decay);
    }

    /// <summary>
    /// Days until predicted recall falls to <paramref name="desiredRetention"/>. At 0.9 this is
    /// exactly the stability; lower retention stretches it. Never less than one day.
    /// </summary>
    public static int IntervalDays(double stability, double desiredRetention)
    {
        var days = stability / Factor * (Math.Pow(desiredRetention, 1 / Decay) - 1);
        return Math.Clamp((int)Math.Round(days, MidpointRounding.AwayFromZero), 1, MaxIntervalDays);
    }

    /// <summary>
    /// Apply a review. <paramref name="retrievability"/> is the predicted recall at the moment of the
    /// review (from <see cref="Retrievability"/>); the harder the recall was predicted to be, the more a
    /// success is worth. A lapse never raises stability.
    /// </summary>
    public static State Next(State current, double retrievability, FsrsGrade grade)
    {
        var r = Math.Clamp(retrievability, 0, 1);
        var d = current.Difficulty;
        var s = Math.Max(current.Stability, MinStability);

        var nextDifficulty = NextDifficulty(d, grade);
        var nextStability = grade == FsrsGrade.Again
            ? Math.Min(s, ForgetStability(d, s, r))
            : RecallStability(d, s, r, grade);

        return new State(nextDifficulty, Math.Max(nextStability, MinStability));
    }

    private static double InitialDifficulty(FsrsGrade grade) =>
        Math.Clamp(W[4] - ((int)grade - 3) * W[5], MinDifficulty, MaxDifficulty);

    private static double NextDifficulty(double d, FsrsGrade grade)
    {
        var stepped = d - W[6] * ((int)grade - 3);
        // Mean reversion toward the initial difficulty of a Good first rating, so difficulty
        // cannot drift to an extreme and stick there.
        var reverted = W[7] * W[4] + (1 - W[7]) * stepped;
        return Math.Clamp(reverted, MinDifficulty, MaxDifficulty);
    }

    private static double RecallStability(double d, double s, double r, FsrsGrade grade)
    {
        var hardPenalty = grade == FsrsGrade.Hard ? W[15] : 1;
        var easyBonus = grade == FsrsGrade.Easy ? W[16] : 1;
        return s * (1 + Math.Exp(W[8]) * (11 - d) * Math.Pow(s, -W[9]) * (Math.Exp((1 - r) * W[10]) - 1) * hardPenalty * easyBonus);
    }

    private static double ForgetStability(double d, double s, double r) =>
        W[11] * Math.Pow(d, -W[12]) * (Math.Pow(s + 1, W[13]) - 1) * Math.Exp((1 - r) * W[14]);
}
