using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class FlashcardPerNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_flashcards_Book_DayNumber",
                table: "flashcards");

            migrationBuilder.DropIndex(
                name: "IX_flashcards_Book_EntryDate",
                table: "flashcards");

            migrationBuilder.DropColumn(
                name: "DayNumber",
                table: "flashcards");

            migrationBuilder.CreateIndex(
                name: "IX_flashcards_Book_EntryDate",
                table: "flashcards",
                columns: new[] { "Book", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_flashcards_EntryDate",
                table: "flashcards",
                column: "EntryDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_flashcards_Book_EntryDate",
                table: "flashcards");

            migrationBuilder.DropIndex(
                name: "IX_flashcards_EntryDate",
                table: "flashcards");

            migrationBuilder.AddColumn<int>(
                name: "DayNumber",
                table: "flashcards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_flashcards_Book_DayNumber",
                table: "flashcards",
                columns: new[] { "Book", "DayNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_flashcards_Book_EntryDate",
                table: "flashcards",
                columns: new[] { "Book", "EntryDate" },
                unique: true);
        }
    }
}
