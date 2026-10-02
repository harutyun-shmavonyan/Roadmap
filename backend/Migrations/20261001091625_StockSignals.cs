using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class StockSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "signal_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsTradingDay = table.Column<bool>(type: "boolean", nullable: false),
                    Summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    MarketsJson = table.Column<string>(type: "text", nullable: true),
                    WatchJson = table.Column<string>(type: "text", nullable: true),
                    Warnings = table.Column<List<string>>(type: "text[]", nullable: false),
                    ReportMarkdown = table.Column<string>(type: "text", nullable: true),
                    GitSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SignalCount = table.Column<int>(type: "integer", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signal_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "signals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SignalRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Market = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Screen = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Headline = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Thesis = table.Column<string>(type: "text", nullable: false),
                    EvidenceJson = table.Column<string>(type: "text", nullable: false),
                    Horizon = table.Column<string>(type: "text", nullable: false),
                    HorizonDays = table.Column<int>(type: "integer", nullable: true),
                    Proposal = table.Column<string>(type: "text", nullable: true),
                    ProposalJson = table.Column<string>(type: "text", nullable: true),
                    CandidatesJson = table.Column<string>(type: "text", nullable: false),
                    ContextJson = table.Column<string>(type: "text", nullable: true),
                    Invalidation = table.Column<string>(type: "text", nullable: true),
                    Regime = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    NextStep = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TriageJson = table.Column<string>(type: "text", nullable: true),
                    TriageSummary = table.Column<string>(type: "text", nullable: true),
                    TriagedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TriageModel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_signals_signal_runs_SignalRunId",
                        column: x => x.SignalRunId,
                        principalTable: "signal_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_signal_runs_RunDate",
                table: "signal_runs",
                column: "RunDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signals_EventId",
                table: "signals",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signals_SignalRunId",
                table: "signals",
                column: "SignalRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "signals");

            migrationBuilder.DropTable(
                name: "signal_runs");
        }
    }
}
