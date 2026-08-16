using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Seeding;

public static class SeedData
{
    public static async Task SeedDemoRoadmap(RoadmapDbContext db)
    {
        if (await db.Roadmaps.AnyAsync()) return;

        db.Roadmaps.Add(new RoadmapDefinition
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "My Roadmap",
            Description = "Personal development roadmap"
        });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Puts a starter set of breakfasts in the Nutrition tab so it opens with something
    /// worth eating rather than an empty page. Runs only while the meal book is empty —
    /// once anything is saved (or everything is deleted and one meal kept), it never
    /// touches the table again.
    /// </summary>
    public static async Task SeedMeals(RoadmapDbContext db)
    {
        if (await db.Meals.AnyAsync()) return;

        var starters = new List<Meal>
        {
            new()
            {
                Id = Guid.NewGuid(), Slot = MealSlot.Breakfast, SortOrder = 0, IsFavorite = true,
                Name = "Greek yogurt, berries & walnuts",
                Summary = "Thirty grams of protein with no cooking at all — the default when the morning is tight.",
                Ingredients =
                [
                    "250 g Greek yogurt (2%)",
                    "100 g mixed berries",
                    "20 g walnuts, roughly broken",
                    "1 tbsp chia seeds",
                    "1 tsp honey (optional)",
                ],
                Steps = ["Spoon the yogurt into a bowl.", "Top with berries, walnuts and chia.", "Drizzle the honey over it."],
                Calories = 405, ProteinG = 30, CarbsG = 35, FatG = 20, PrepMinutes = 3,
                Tags = ["high-protein", "no-cook", "3-min"],
            },
            new()
            {
                Id = Guid.NewGuid(), Slot = MealSlot.Breakfast, SortOrder = 1, IsFavorite = true,
                Name = "Spinach & feta omelette with rye",
                Summary = "Eggs plus greens: high satiety, low carb, and it keeps hunger away until lunch.",
                Ingredients =
                [
                    "3 whole eggs",
                    "60 g spinach",
                    "30 g feta, crumbled",
                    "1 tsp olive oil",
                    "1 slice rye bread",
                    "Salt, black pepper",
                ],
                Steps =
                [
                    "Wilt the spinach in the oiled pan, 1 minute.",
                    "Beat the eggs with salt and pepper, pour over the spinach.",
                    "Scatter the feta on top, fold once the base is set.",
                    "Serve with the rye.",
                ],
                Calories = 430, ProteinG = 26, CarbsG = 20, FatG = 27, PrepMinutes = 10,
                Tags = ["high-protein", "low-carb", "veggie"],
            },
            new()
            {
                Id = Guid.NewGuid(), Slot = MealSlot.Breakfast, SortOrder = 2,
                Name = "Overnight oats with whey & banana",
                Summary = "Built the night before. The biggest breakfast here — best on training mornings.",
                Ingredients =
                [
                    "60 g rolled oats",
                    "200 ml milk",
                    "1 scoop (30 g) whey protein",
                    "1 banana, sliced",
                    "1 tbsp peanut butter",
                ],
                Steps =
                [
                    "Stir oats, milk and whey in a jar until smooth.",
                    "Refrigerate overnight.",
                    "Top with the banana and peanut butter in the morning.",
                ],
                Calories = 650, ProteinG = 43, CarbsG = 82, FatG = 17, PrepMinutes = 5,
                Tags = ["make-ahead", "high-protein", "pre-workout"],
            },
            new()
            {
                Id = Guid.NewGuid(), Slot = MealSlot.Breakfast, SortOrder = 3,
                Name = "Cottage cheese & avocado rye toast",
                Summary = "Slow casein protein and good fat on one plate — steady energy, no crash.",
                Ingredients =
                [
                    "200 g cottage cheese",
                    "2 slices rye bread",
                    "1/2 avocado",
                    "1 tomato, sliced",
                    "Salt, black pepper, chilli flakes",
                ],
                Steps =
                [
                    "Toast the rye.",
                    "Spread the avocado, then spoon the cottage cheese over it.",
                    "Lay the tomato on top and season.",
                ],
                Calories = 460, ProteinG = 32, CarbsG = 40, FatG = 16, PrepMinutes = 6,
                Tags = ["high-protein", "no-cook", "veggie"],
            },
            new()
            {
                Id = Guid.NewGuid(), Slot = MealSlot.Breakfast, SortOrder = 4,
                Name = "Smoked salmon & avocado open sandwich",
                Summary = "The omega-3 breakfast. Worth two or three mornings a week for the fats alone.",
                Ingredients =
                [
                    "80 g smoked salmon",
                    "2 slices rye bread",
                    "1/2 avocado",
                    "1 tbsp cream cheese",
                    "Lemon, dill, black pepper",
                ],
                Steps =
                [
                    "Spread the cream cheese on the rye.",
                    "Layer the avocado, then the salmon.",
                    "Finish with lemon, dill and pepper.",
                ],
                Calories = 430, ProteinG = 26, CarbsG = 32, FatG = 18, PrepMinutes = 5,
                Tags = ["omega-3", "no-cook", "high-protein"],
            },
            new()
            {
                Id = Guid.NewGuid(), Slot = MealSlot.Breakfast, SortOrder = 5,
                Name = "Shakshuka for one",
                Summary = "A weekend breakfast: two eggs poached in tomato, heavy on vegetables and light on effort.",
                Ingredients =
                [
                    "2 eggs",
                    "200 g chopped tomatoes",
                    "1/2 onion, diced",
                    "1 garlic clove, sliced",
                    "1 tsp olive oil",
                    "1/2 tsp paprika, pinch of cumin",
                    "1 slice rye bread, to mop",
                ],
                Steps =
                [
                    "Soften the onion and garlic in the oil, 4 minutes.",
                    "Add the tomatoes and spices, simmer 6 minutes.",
                    "Make two wells, crack in the eggs, cover and cook 5 minutes.",
                    "Serve straight from the pan with the rye.",
                ],
                Calories = 400, ProteinG = 20, CarbsG = 28, FatG = 22, PrepMinutes = 20,
                Tags = ["weekend", "veggie", "one-pan"],
            },
        };

        db.Meals.AddRange(starters);
        await db.SaveChangesAsync();
    }
}
