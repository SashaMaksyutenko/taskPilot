using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskpilot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "ProjectTasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: give every existing task a per-project sequential number (oldest first).
            migrationBuilder.Sql(@"
                UPDATE ""ProjectTasks"" AS t
                SET ""Number"" = s.rn
                FROM (
                    SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""ProjectId"" ORDER BY ""CreatedAt"", ""Id"") AS rn
                    FROM ""ProjectTasks""
                ) AS s
                WHERE t.""Id"" = s.""Id"";
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_ProjectId_Number",
                table: "ProjectTasks",
                columns: new[] { "ProjectId", "Number" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectTasks_ProjectId_Number",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "ProjectTasks");
        }
    }
}
