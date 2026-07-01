using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskpilot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillEmptyMemberRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The AddProjectMemberRole migration defaulted existing rows to an empty string;
            // treat those legacy collaborators as full-access Editors.
            migrationBuilder.Sql("UPDATE \"ProjectMembers\" SET \"Role\" = 'Editor' WHERE \"Role\" IS NULL OR \"Role\" = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
