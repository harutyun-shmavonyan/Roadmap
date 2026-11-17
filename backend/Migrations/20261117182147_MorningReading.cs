using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class MorningReading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reading_editions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    CoveredUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reading_editions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reading_evaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reading_evaluations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reading_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reading_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reading_feedback",
                columns: table => new
                {
                    ContentKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reading_feedback", x => x.ContentKey);
                    table.ForeignKey(
                        name: "FK_reading_feedback_reading_evaluations_EvaluationId",
                        column: x => x.EvaluationId,
                        principalTable: "reading_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reading_editions_IssueDate",
                table: "reading_editions",
                column: "IssueDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reading_evaluations_ContentKey",
                table: "reading_evaluations",
                column: "ContentKey");

            migrationBuilder.CreateIndex(
                name: "IX_reading_evaluations_CreatedAt",
                table: "reading_evaluations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_reading_evaluations_EvaluationKey",
                table: "reading_evaluations",
                column: "EvaluationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reading_feedback_EvaluationId",
                table: "reading_feedback",
                column: "EvaluationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reading_editions");

            migrationBuilder.DropTable(
                name: "reading_feedback");

            migrationBuilder.DropTable(
                name: "reading_settings");

            migrationBuilder.DropTable(
                name: "reading_evaluations");
        }
    }
}
