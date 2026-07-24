using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskpilot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectMemberMute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Muted",
                table: "ProjectMembers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Muted",
                table: "ProjectMembers");
        }
    }
}
