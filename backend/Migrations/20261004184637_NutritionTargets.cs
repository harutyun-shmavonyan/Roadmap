using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class NutritionTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nutrition_targets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Calories = table.Column<double>(type: "double precision", nullable: true),
                    ProteinG = table.Column<double>(type: "double precision", nullable: true),
                    CarbsG = table.Column<double>(type: "double precision", nullable: true),
                    FatG = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nutrition_targets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_nutrition_targets_EffectiveFrom",
                table: "nutrition_targets",
                column: "EffectiveFrom",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "nutrition_targets");
        }
    }
}
