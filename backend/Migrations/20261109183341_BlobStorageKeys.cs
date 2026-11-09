using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class BlobStorageKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "meal_images",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "meal_images",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CvSizeBytes",
                table: "job_postings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "CvStorageKey",
                table: "job_postings",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "experience_images",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "experience_images",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "article_images",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "article_images",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "meal_images");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "meal_images");

            migrationBuilder.DropColumn(
                name: "CvSizeBytes",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "CvStorageKey",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "experience_images");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "experience_images");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "article_images");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "article_images");
        }
    }
}
