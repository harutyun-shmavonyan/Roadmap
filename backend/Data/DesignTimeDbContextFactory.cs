using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Roadmap.Api.Data;

/// <summary>
/// Design-time factory used only by `dotnet ef` (migrations). Building the context this way
/// keeps EF tooling from running Program.cs — which auto-migrates and seeds against a live
/// database on startup. The connection string here is never opened; it only tells the Npgsql
/// provider how to shape SQL for migrations.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RoadmapDbContext>
{
    public RoadmapDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RoadmapDbContext>()
            .UseNpgsql("Host=localhost;Database=roadmap_design;Username=postgres;Password=postgres")
            .Options;
        return new RoadmapDbContext(options);
    }
}
