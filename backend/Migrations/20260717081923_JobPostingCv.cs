using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class JobPostingCv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CvChangeList",
                table: "job_postings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "TailoredCvPdf",
                table: "job_postings",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CvChangeList",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "TailoredCvPdf",
                table: "job_postings");
        }
    }
}
