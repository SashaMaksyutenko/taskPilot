using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskpilot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskEscalationLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EscalationLevel",
                table: "ProjectTasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Treat tasks already escalated under the old single-tier scheme as fully
            // escalated, so the new multi-tier check doesn't re-notify them.
            migrationBuilder.Sql(
                "UPDATE \"ProjectTasks\" SET \"EscalationLevel\" = 3 WHERE \"EscalatedAt\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EscalationLevel",
                table: "ProjectTasks");
        }
    }
}
