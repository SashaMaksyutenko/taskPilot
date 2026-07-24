using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskpilot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillEndorsements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SkillEndorsements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Skill = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EndorserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillEndorsements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SkillEndorsements_UserId",
                table: "SkillEndorsements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillEndorsements_UserId_Skill_EndorserId",
                table: "SkillEndorsements",
                columns: new[] { "UserId", "Skill", "EndorserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SkillEndorsements");
        }
    }
}
