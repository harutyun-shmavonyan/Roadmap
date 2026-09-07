using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class JobApplicationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationNotes",
                table: "job_postings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationStatus",
                table: "job_postings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AppliedAt",
                table: "job_postings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RespondedAt",
                table: "job_postings",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationNotes",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "ApplicationStatus",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "AppliedAt",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "job_postings");
        }
    }
}
