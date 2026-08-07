using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskpilot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GitHubRepo",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHubWebhookSecret",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GitHubTaskLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubTaskLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitHubTaskLinks_ProjectTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GitHubTaskLinks_TaskId_Kind_ExternalId",
                table: "GitHubTaskLinks",
                columns: new[] { "TaskId", "Kind", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitHubTaskLinks");

            migrationBuilder.DropColumn(
                name: "GitHubRepo",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "GitHubWebhookSecret",
                table: "Projects");
        }
    }
}
