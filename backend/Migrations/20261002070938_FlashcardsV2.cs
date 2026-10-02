using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class FlashcardsV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "flashcards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Book = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DayNumber = table.Column<int>(type: "integer", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flashcards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "flashcard_prompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlashcardId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_flashcard_prompts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_flashcard_prompts_flashcards_FlashcardId",
                        column: x => x.FlashcardId,
                        principalTable: "flashcards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flashcard_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlashcardPromptId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_flashcard_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_flashcard_reviews_flashcard_prompts_FlashcardPromptId",
                        column: x => x.FlashcardPromptId,
                        principalTable: "flashcard_prompts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_flashcard_prompts_FlashcardId_SortOrder",
                table: "flashcard_prompts",
                columns: new[] { "FlashcardId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_flashcard_prompts_State_DueOn",
                table: "flashcard_prompts",
                columns: new[] { "State", "DueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_flashcard_reviews_FlashcardPromptId_ReviewedAt",
                table: "flashcard_reviews",
                columns: new[] { "FlashcardPromptId", "ReviewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_flashcard_reviews_ReviewDate",
                table: "flashcard_reviews",
                column: "ReviewDate");

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
        

            // Seed v2 from v1 — a one-time COPY, not a link. Every note becomes a card with a fresh id, and
            // the prompts written for the first, note-bound attempt move onto the cards with their schedule
            // intact; then the note-bound tables go. Nothing in flashcards points back at notes afterwards.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE fc_map AS SELECT n."Id" AS note_id, gen_random_uuid() AS card_id FROM notes n;
                INSERT INTO flashcards ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
                SELECT m.card_id, n."Book", n."DayNumber", n."EntryDate", n."Content", n."CreatedAt", n."UpdatedAt"
                FROM notes n JOIN fc_map m ON m.note_id = n."Id";
                INSERT INTO flashcard_prompts ("Id","FlashcardId","Question","Answer","SortOrder","State","Difficulty","Stability",
                    "DueOn","LastReviewedAt","Relearning","Lapses","Reviews","CreatedAt","UpdatedAt")
                SELECT p."Id", m.card_id, p."Question", p."Answer", p."SortOrder", p."State", p."Difficulty", p."Stability",
                    p."DueOn", p."LastReviewedAt", p."Relearning", p."Lapses", p."Reviews", p."CreatedAt", p."UpdatedAt"
                FROM note_prompts p JOIN fc_map m ON m.note_id = p."NoteId";
                INSERT INTO flashcard_reviews ("Id","FlashcardPromptId","ReviewedAt","ReviewDate","Grade","ElapsedDays","Retrievability",
                    "WasRelearning","StabilityBefore","StabilityAfter","DifficultyBefore","DifficultyAfter","Answer","Note")
                SELECT r."Id", r."NotePromptId", r."ReviewedAt", r."ReviewDate", r."Grade", r."ElapsedDays", r."Retrievability",
                    r."WasRelearning", r."StabilityBefore", r."StabilityAfter", r."DifficultyBefore", r."DifficultyAfter", r."Answer", r."Note"
                FROM note_prompt_reviews r;
                DROP TABLE fc_map;
                """);

            migrationBuilder.DropTable(
                name: "note_prompt_reviews");

            migrationBuilder.DropTable(
                name: "note_prompts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "flashcard_reviews");

            migrationBuilder.DropTable(
                name: "flashcard_prompts");

            migrationBuilder.DropTable(
                name: "flashcards");

            migrationBuilder.CreateTable(
                name: "note_prompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Difficulty = table.Column<double>(type: "double precision", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Lapses = table.Column<int>(type: "integer", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    Relearning = table.Column<bool>(type: "boolean", nullable: false),
                    Reviews = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Stability = table.Column<double>(type: "double precision", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
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
                    Answer = table.Column<string>(type: "text", nullable: true),
                    DifficultyAfter = table.Column<double>(type: "double precision", nullable: false),
                    DifficultyBefore = table.Column<double>(type: "double precision", nullable: false),
                    ElapsedDays = table.Column<int>(type: "integer", nullable: false),
                    Grade = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    Retrievability = table.Column<double>(type: "double precision", nullable: false),
                    ReviewDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StabilityAfter = table.Column<double>(type: "double precision", nullable: false),
                    StabilityBefore = table.Column<double>(type: "double precision", nullable: false),
                    WasRelearning = table.Column<bool>(type: "boolean", nullable: false)
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
    }
}
