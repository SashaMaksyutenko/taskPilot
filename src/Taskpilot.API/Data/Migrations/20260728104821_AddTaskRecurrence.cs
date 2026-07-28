using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskpilot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing tasks don't recur: interval 1 (the sane default) and type "None" so EF can
            // read the enum back (an empty string would fail to parse).
            migrationBuilder.AddColumn<int>(
                name: "RecurrenceInterval",
                table: "ProjectTasks",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RecurrenceType",
                table: "ProjectTasks",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "None");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecurrenceInterval",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "RecurrenceType",
                table: "ProjectTasks");
        }
    }
}
