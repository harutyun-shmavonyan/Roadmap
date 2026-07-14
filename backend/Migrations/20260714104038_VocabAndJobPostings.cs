using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class VocabAndJobPostings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Queries = table.Column<List<string>>(type: "text[]", nullable: false),
                    MaxAgeDays = table.Column<int>(type: "integer", nullable: false),
                    RawCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vocab_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Term = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedTerm = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Definition = table.Column<string>(type: "text", nullable: false),
                    GlossHy = table.Column<string>(type: "text", nullable: true),
                    GlossRu = table.Column<string>(type: "text", nullable: true),
                    Frequency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Register = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Examples = table.Column<List<string>>(type: "text[]", nullable: false),
                    Collocations = table.Column<List<string>>(type: "text[]", nullable: false),
                    Synonyms = table.Column<List<string>>(type: "text[]", nullable: false),
                    MemoryHook = table.Column<string>(type: "text", nullable: true),
                    SourceContext = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Repetitions = table.Column<int>(type: "integer", nullable: false),
                    EaseFactor = table.Column<double>(type: "double precision", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Lapses = table.Column<int>(type: "integer", nullable: false),
                    TotalReviews = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocab_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_postings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Company = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Location = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PostedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Bucket = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SeniorityClass = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AiKeywordHits = table.Column<int>(type: "integer", nullable: false),
                    GeoHints = table.Column<List<string>>(type: "text[]", nullable: false),
                    Queries = table.Column<List<string>>(type: "text[]", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: true),
                    Reasoning = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_postings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_postings_job_runs_JobRunId",
                        column: x => x.JobRunId,
                        principalTable: "job_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vocab_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VocabEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Grade = table.Column<int>(type: "integer", nullable: false),
                    PromptType = table.Column<string>(type: "text", nullable: true),
                    Answer = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    IntervalBefore = table.Column<int>(type: "integer", nullable: false),
                    IntervalAfter = table.Column<int>(type: "integer", nullable: false),
                    EaseBefore = table.Column<double>(type: "double precision", nullable: false),
                    EaseAfter = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocab_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vocab_reviews_vocab_entries_VocabEntryId",
                        column: x => x.VocabEntryId,
                        principalTable: "vocab_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_postings_JobRunId_SortOrder",
                table: "job_postings",
                columns: new[] { "JobRunId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_job_runs_RunDate",
                table: "job_runs",
                column: "RunDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vocab_entries_DueOn",
                table: "vocab_entries",
                column: "DueOn");

            migrationBuilder.CreateIndex(
                name: "IX_vocab_entries_NormalizedTerm",
                table: "vocab_entries",
                column: "NormalizedTerm",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vocab_reviews_VocabEntryId_ReviewedAt",
                table: "vocab_reviews",
                columns: new[] { "VocabEntryId", "ReviewedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_postings");

            migrationBuilder.DropTable(
                name: "vocab_reviews");

            migrationBuilder.DropTable(
                name: "job_runs");

            migrationBuilder.DropTable(
                name: "vocab_entries");
        }
    }
}
