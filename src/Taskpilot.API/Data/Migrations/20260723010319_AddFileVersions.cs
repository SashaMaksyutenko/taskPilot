using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskpilot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PreviousVersionId",
                table: "FileAttachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "FileAttachments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_FileAttachments_PreviousVersionId",
                table: "FileAttachments",
                column: "PreviousVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileAttachments_FileAttachments_PreviousVersionId",
                table: "FileAttachments",
                column: "PreviousVersionId",
                principalTable: "FileAttachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileAttachments_FileAttachments_PreviousVersionId",
                table: "FileAttachments");

            migrationBuilder.DropIndex(
                name: "IX_FileAttachments_PreviousVersionId",
                table: "FileAttachments");

            migrationBuilder.DropColumn(
                name: "PreviousVersionId",
                table: "FileAttachments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "FileAttachments");
        }
    }
}
