using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class NodeSubPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "node_subpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_node_subpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_node_subpoints_roadmap_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "roadmap_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "node_subpoint_checks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubPointId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_node_subpoint_checks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_node_subpoint_checks_node_subpoints_SubPointId",
                        column: x => x.SubPointId,
                        principalTable: "node_subpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_node_subpoint_checks_SubPointId_Date",
                table: "node_subpoint_checks",
                columns: new[] { "SubPointId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_node_subpoints_NodeId_SortOrder",
                table: "node_subpoints",
                columns: new[] { "NodeId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "node_subpoint_checks");

            migrationBuilder.DropTable(
                name: "node_subpoints");
        }
    }
}
