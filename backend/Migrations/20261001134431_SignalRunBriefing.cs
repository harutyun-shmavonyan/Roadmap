using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class SignalRunBriefing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Briefing",
                table: "signal_runs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Briefing",
                table: "signal_runs");
        }
    }
}
