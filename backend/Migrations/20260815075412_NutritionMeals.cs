using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class NutritionMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Ingredients = table.Column<List<string>>(type: "text[]", nullable: false),
                    Steps = table.Column<List<string>>(type: "text[]", nullable: false),
                    Calories = table.Column<int>(type: "integer", nullable: true),
                    ProteinG = table.Column<int>(type: "integer", nullable: true),
                    CarbsG = table.Column<int>(type: "integer", nullable: true),
                    FatG = table.Column<int>(type: "integer", nullable: true),
                    PrepMinutes = table.Column<int>(type: "integer", nullable: true),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_meals_Slot_SortOrder",
                table: "meals",
                columns: new[] { "Slot", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meals");
        }
    }
}
