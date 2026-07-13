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
}
