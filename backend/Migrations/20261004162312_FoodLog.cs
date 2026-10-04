using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class FoodLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "food_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Slot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MealId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Servings = table.Column<double>(type: "double precision", nullable: false),
                    Calories = table.Column<double>(type: "double precision", nullable: true),
                    ProteinG = table.Column<double>(type: "double precision", nullable: true),
                    CarbsG = table.Column<double>(type: "double precision", nullable: true),
                    FatG = table.Column<double>(type: "double precision", nullable: true),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_food_log_meals_MealId",
                        column: x => x.MealId,
                        principalTable: "meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_food_log_Date",
                table: "food_log",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_food_log_MealId",
                table: "food_log",
                column: "MealId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "food_log");
        }
    }
}
