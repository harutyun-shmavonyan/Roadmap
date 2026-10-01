using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class NotePrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "note_prompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Difficulty = table.Column<double>(type: "double precision", nullable: false),
                    Stability = table.Column<double>(type: "double precision", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Relearning = table.Column<bool>(type: "boolean", nullable: false),
                    Lapses = table.Column<int>(type: "integer", nullable: false),
                    Reviews = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_prompts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_note_prompts_notes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "note_prompt_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotePromptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Grade = table.Column<int>(type: "integer", nullable: false),
                    ElapsedDays = table.Column<int>(type: "integer", nullable: false),
                    Retrievability = table.Column<double>(type: "double precision", nullable: false),
                    WasRelearning = table.Column<bool>(type: "boolean", nullable: false),
                    StabilityBefore = table.Column<double>(type: "double precision", nullable: false),
                    StabilityAfter = table.Column<double>(type: "double precision", nullable: false),
                    DifficultyBefore = table.Column<double>(type: "double precision", nullable: false),
                    DifficultyAfter = table.Column<double>(type: "double precision", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_prompt_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_note_prompt_reviews_note_prompts_NotePromptId",
                        column: x => x.NotePromptId,
                        principalTable: "note_prompts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_note_prompt_reviews_NotePromptId_ReviewedAt",
                table: "note_prompt_reviews",
                columns: new[] { "NotePromptId", "ReviewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_note_prompt_reviews_ReviewDate",
                table: "note_prompt_reviews",
                column: "ReviewDate");

            migrationBuilder.CreateIndex(
                name: "IX_note_prompts_NoteId_SortOrder",
                table: "note_prompts",
                columns: new[] { "NoteId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_note_prompts_State_DueOn",
                table: "note_prompts",
                columns: new[] { "State", "DueOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "note_prompt_reviews");

            migrationBuilder.DropTable(
                name: "note_prompts");
        }
    }
}
