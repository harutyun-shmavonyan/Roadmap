using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class CourseSlugUniqueAmongLive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_courses_Slug",
                table: "courses");

            migrationBuilder.CreateIndex(
                name: "IX_courses_Slug",
                table: "courses",
                column: "Slug",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_courses_Slug",
                table: "courses");

            migrationBuilder.CreateIndex(
                name: "IX_courses_Slug",
                table: "courses",
                column: "Slug",
                unique: true);
        }
    }
}
