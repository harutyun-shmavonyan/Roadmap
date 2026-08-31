namespace Roadmap.Api.Entities;

/// <summary>
/// How a sprint prices earned points.
///
/// Fixed — the original model: a unit of work is always worth the item's PointsPerUnit,
/// however lopsided the sprint gets.
///
/// Weighted — each day keeps its preplanned point total, but that budget is re-split among
/// the day's items by how much of its sprint commitment each one has already consumed:
/// an untouched item's work is at full weight (1.0), one at 100% of its commitment at 0.5,
/// and past 200% at 0. Weights are evaluated at the start of each day, so prices hold for
/// the whole day. Work on committed items outside their planned day earns weight × nominal
/// rate; uncommitted (bonus) work is untouched and earns nominal, as in Fixed.
/// </summary>
public enum ScoringMode
{
    Fixed,
    Weighted
}
