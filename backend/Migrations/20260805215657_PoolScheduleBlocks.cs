using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class PoolScheduleBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "NodeId",
                table: "sprint_plan_entries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "BlockId",
                table: "sprint_plan_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "schedule_blocks",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Queue");

            migrationBuilder.AddColumn<bool>(
                name: "IsActiveInBlock",
                table: "roadmap_nodes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_sprint_plan_entries_BlockId",
                table: "sprint_plan_entries",
                column: "BlockId");

            migrationBuilder.AddForeignKey(
                name: "FK_sprint_plan_entries_schedule_blocks_BlockId",
                table: "sprint_plan_entries",
                column: "BlockId",
                principalTable: "schedule_blocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sprint_plan_entries_schedule_blocks_BlockId",
                table: "sprint_plan_entries");

            migrationBuilder.DropIndex(
                name: "IX_sprint_plan_entries_BlockId",
                table: "sprint_plan_entries");

            migrationBuilder.DropColumn(
                name: "BlockId",
                table: "sprint_plan_entries");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "schedule_blocks");

            migrationBuilder.DropColumn(
                name: "IsActiveInBlock",
                table: "roadmap_nodes");

            migrationBuilder.AlterColumn<Guid>(
                name: "NodeId",
                table: "sprint_plan_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
