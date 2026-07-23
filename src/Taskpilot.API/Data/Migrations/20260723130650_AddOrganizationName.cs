using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskpilot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "OrganizationSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "TaskPilot");

            migrationBuilder.UpdateData(
                table: "OrganizationSettings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-0000000005e7"),
                column: "Name",
                value: "TaskPilot");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "OrganizationSettings");
        }
    }
}
