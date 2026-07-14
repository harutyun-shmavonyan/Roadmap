using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChecklistOnNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These two objects are never created by any migration in this repo — the
            // history they came from was squashed into `Latest`, which doesn't include
            // them. Plain DropTable/DropColumn therefore fail on a fresh database and
            // the whole chain can't bootstrap. IF EXISTS makes this a no-op for a new DB
            // while staying identical for databases that already applied it.
            migrationBuilder.Sql("DROP TABLE IF EXISTS task_subpoints;");
            migrationBuilder.Sql("ALTER TABLE single_tasks DROP COLUMN IF EXISTS \"IsChecklist\";");

            migrationBuilder.AddColumn<bool>(
                name: "IsChecklist",
                table: "roadmap_nodes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsChecklist",
                table: "roadmap_nodes");

            migrationBuilder.AddColumn<bool>(
                name: "IsChecklist",
                table: "single_tasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "task_subpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_subpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_subpoints_single_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "single_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_subpoints_TaskId_SortOrder",
                table: "task_subpoints",
                columns: new[] { "TaskId", "SortOrder" });
        }
    }
}
