using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class JobPostingCvFit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CvFitGaps",
                table: "job_postings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CvFitScore",
                table: "job_postings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CvFitGaps",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "CvFitScore",
                table: "job_postings");
        }
    }
}
