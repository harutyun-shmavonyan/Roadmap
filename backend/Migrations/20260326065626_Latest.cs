using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class Latest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "roadmaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roadmaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "custom_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoadmapId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Points = table.Column<double>(type: "double precision", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_custom_logs_roadmaps_RoadmapId",
                        column: x => x.RoadmapId,
                        principalTable: "roadmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "day_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoadmapId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_day_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_day_plans_roadmaps_RoadmapId",
                        column: x => x.RoadmapId,
                        principalTable: "roadmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "habits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoadmapId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_habits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_habits_roadmaps_RoadmapId",
                        column: x => x.RoadmapId,
                        principalTable: "roadmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "relax_days",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoadmapId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relax_days", x => x.Id);
                    table.ForeignKey(
                        name: "FK_relax_days_roadmaps_RoadmapId",
                        column: x => x.RoadmapId,
                        principalTable: "roadmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "schedule_blocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoadmapId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ScheduleTemplate = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_blocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_schedule_blocks_roadmaps_RoadmapId",
                        column: x => x.RoadmapId,
                        principalTable: "roadmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "single_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoadmapId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EstimatedHours = table.Column<double>(type: "double precision", nullable: false),
                    Weekdays = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DelayedUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_single_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_single_tasks_roadmaps_RoadmapId",
                        column: x => x.RoadmapId,
                        principalTable: "roadmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoadmapId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsStarted = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RelaxDays = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sprints_roadmaps_RoadmapId",
                        column: x => x.RoadmapId,
                        principalTable: "roadmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "week_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoadmapId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_week_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_week_plans_roadmaps_RoadmapId",
                        column: x => x.RoadmapId,
                        principalTable: "roadmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roadmap_nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoadmapId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsActionable = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "NotStarted"),
                    Unit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TotalSize = table.Column<double>(type: "double precision", nullable: true),
                    UnitsPerHour = table.Column<double>(type: "double precision", nullable: true),
                    PointsPerUnit = table.Column<double>(type: "double precision", nullable: true),
                    ScheduleTemplate = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ScheduleBlockId = table.Column<Guid>(type: "uuid", nullable: true),
                    BlockSortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roadmap_nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_roadmap_nodes_roadmap_nodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "roadmap_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_roadmap_nodes_roadmaps_RoadmapId",
                        column: x => x.RoadmapId,
                        principalTable: "roadmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_roadmap_nodes_schedule_blocks_ScheduleBlockId",
                        column: x => x.ScheduleBlockId,
                        principalTable: "schedule_blocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "sprint_goals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Unit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TargetAmount = table.Column<double>(type: "double precision", nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sprint_goals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sprint_goals_sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sprint_habits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    HabitId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPaused = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sprint_habits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sprint_habits_habits_HabitId",
                        column: x => x.HabitId,
                        principalTable: "habits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sprint_habits_sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "day_plan_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DayPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartMinute = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ActualMinutes = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_day_plan_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_day_plan_entries_day_plans_DayPlanId",
                        column: x => x.DayPlanId,
                        principalTable: "day_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_day_plan_entries_roadmap_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "roadmap_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "node_category_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_node_category_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_node_category_links_roadmap_nodes_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "roadmap_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_node_category_links_roadmap_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "roadmap_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sprint_plan_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartMinute = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    PlannedUnits = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sprint_plan_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sprint_plan_entries_roadmap_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "roadmap_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sprint_plan_entries_sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "status_changes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoadmapId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Trigger = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_status_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_status_changes_roadmap_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "roadmap_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_status_changes_roadmaps_RoadmapId",
                        column: x => x.RoadmapId,
                        principalTable: "roadmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoadmapId = table.Column<Guid>(type: "uuid", nullable: false),
                    SprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    Note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_logs_roadmap_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "roadmap_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_logs_roadmaps_RoadmapId",
                        column: x => x.RoadmapId,
                        principalTable: "roadmaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_logs_sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sprint_goal_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SprintGoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sprint_goal_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sprint_goal_logs_sprint_goals_SprintGoalId",
                        column: x => x.SprintGoalId,
                        principalTable: "sprint_goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "week_plan_goals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    TargetDescription = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TargetAmount = table.Column<double>(type: "double precision", nullable: true),
                    ResultAmount = table.Column<double>(type: "double precision", nullable: true),
                    ResultNote = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    SprintGoalId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_week_plan_goals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_week_plan_goals_sprint_goals_SprintGoalId",
                        column: x => x.SprintGoalId,
                        principalTable: "sprint_goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_week_plan_goals_week_plans_WeekPlanId",
                        column: x => x.WeekPlanId,
                        principalTable: "week_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "habit_checks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SprintHabitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsChecked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_habit_checks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_habit_checks_sprint_habits_SprintHabitId",
                        column: x => x.SprintHabitId,
                        principalTable: "sprint_habits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_custom_logs_RoadmapId_Date",
                table: "custom_logs",
                columns: new[] { "RoadmapId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_day_plan_entries_DayPlanId_StartMinute",
                table: "day_plan_entries",
                columns: new[] { "DayPlanId", "StartMinute" });

            migrationBuilder.CreateIndex(
                name: "IX_day_plan_entries_NodeId",
                table: "day_plan_entries",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_day_plans_RoadmapId_Date",
                table: "day_plans",
                columns: new[] { "RoadmapId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_habit_checks_SprintHabitId_Date",
                table: "habit_checks",
                columns: new[] { "SprintHabitId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_habits_RoadmapId",
                table: "habits",
                column: "RoadmapId");

            migrationBuilder.CreateIndex(
                name: "IX_node_category_links_CategoryId",
                table: "node_category_links",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_node_category_links_NodeId_CategoryId",
                table: "node_category_links",
                columns: new[] { "NodeId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_relax_days_RoadmapId_Date",
                table: "relax_days",
                columns: new[] { "RoadmapId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_nodes_ParentId",
                table: "roadmap_nodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_nodes_RoadmapId_ParentId_SortOrder",
                table: "roadmap_nodes",
                columns: new[] { "RoadmapId", "ParentId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_roadmap_nodes_ScheduleBlockId_BlockSortOrder",
                table: "roadmap_nodes",
                columns: new[] { "ScheduleBlockId", "BlockSortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_blocks_RoadmapId_SortOrder",
                table: "schedule_blocks",
                columns: new[] { "RoadmapId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_single_tasks_RoadmapId_IsCompleted_Priority",
                table: "single_tasks",
                columns: new[] { "RoadmapId", "IsCompleted", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_sprint_goal_logs_SprintGoalId_Date",
                table: "sprint_goal_logs",
                columns: new[] { "SprintGoalId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_sprint_goals_SprintId_SortOrder",
                table: "sprint_goals",
                columns: new[] { "SprintId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_sprint_habits_HabitId",
                table: "sprint_habits",
                column: "HabitId");

            migrationBuilder.CreateIndex(
                name: "IX_sprint_habits_SprintId_HabitId",
                table: "sprint_habits",
                columns: new[] { "SprintId", "HabitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sprint_plan_entries_NodeId",
                table: "sprint_plan_entries",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_sprint_plan_entries_SprintId_Date",
                table: "sprint_plan_entries",
                columns: new[] { "SprintId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_sprints_RoadmapId_StartDate",
                table: "sprints",
                columns: new[] { "RoadmapId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_status_changes_NodeId",
                table: "status_changes",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_status_changes_RoadmapId_NodeId_ChangedAt",
                table: "status_changes",
                columns: new[] { "RoadmapId", "NodeId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_week_plan_goals_SprintGoalId",
                table: "week_plan_goals",
                column: "SprintGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_week_plan_goals_WeekPlanId_SortOrder",
                table: "week_plan_goals",
                columns: new[] { "WeekPlanId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_week_plans_RoadmapId_WeekStart",
                table: "week_plans",
                columns: new[] { "RoadmapId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_logs_NodeId",
                table: "work_logs",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_work_logs_RoadmapId_NodeId_Date",
                table: "work_logs",
                columns: new[] { "RoadmapId", "NodeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_work_logs_SprintId_NodeId_Date",
                table: "work_logs",
                columns: new[] { "SprintId", "NodeId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custom_logs");

            migrationBuilder.DropTable(
                name: "day_plan_entries");

            migrationBuilder.DropTable(
                name: "habit_checks");

            migrationBuilder.DropTable(
                name: "node_category_links");

            migrationBuilder.DropTable(
                name: "relax_days");

            migrationBuilder.DropTable(
                name: "single_tasks");

            migrationBuilder.DropTable(
                name: "sprint_goal_logs");

            migrationBuilder.DropTable(
                name: "sprint_plan_entries");

            migrationBuilder.DropTable(
                name: "status_changes");

            migrationBuilder.DropTable(
                name: "week_plan_goals");

            migrationBuilder.DropTable(
                name: "work_logs");

            migrationBuilder.DropTable(
                name: "day_plans");

            migrationBuilder.DropTable(
                name: "sprint_habits");

            migrationBuilder.DropTable(
                name: "sprint_goals");

            migrationBuilder.DropTable(
                name: "week_plans");

            migrationBuilder.DropTable(
                name: "roadmap_nodes");

            migrationBuilder.DropTable(
                name: "habits");

            migrationBuilder.DropTable(
                name: "sprints");

            migrationBuilder.DropTable(
                name: "schedule_blocks");

            migrationBuilder.DropTable(
                name: "roadmaps");
        }
    }
}
