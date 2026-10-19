using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class TaskProjectsAndDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "single_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "task_dependencies",
                columns: table => new
                {
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependsOnTaskId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_dependencies", x => new { x.TaskId, x.DependsOnTaskId });
                    table.CheckConstraint("CK_task_dependencies_not_self", "\"TaskId\" <> \"DependsOnTaskId\"");
                    table.ForeignKey(
                        name: "FK_task_dependencies_single_tasks_DependsOnTaskId",
                        column: x => x.DependsOnTaskId,
                        principalTable: "single_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_dependencies_single_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "single_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoadmapId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_projects_roadmaps_RoadmapId",
                        column: x => x.RoadmapId,
                        principalTable: "roadmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_single_tasks_ProjectId",
                table: "single_tasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_task_dependencies_DependsOnTaskId",
                table: "task_dependencies",
                column: "DependsOnTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_task_projects_RoadmapId_Name",
                table: "task_projects",
                columns: new[] { "RoadmapId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_single_tasks_task_projects_ProjectId",
                table: "single_tasks",
                column: "ProjectId",
                principalTable: "task_projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_single_tasks_task_projects_ProjectId",
                table: "single_tasks");

            migrationBuilder.DropTable(
                name: "task_dependencies");

            migrationBuilder.DropTable(
                name: "task_projects");

            migrationBuilder.DropIndex(
                name: "IX_single_tasks_ProjectId",
                table: "single_tasks");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "single_tasks");
        }
    }
}
