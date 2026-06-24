using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskpilot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FileAttachmentId",
                table: "Messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_FileAttachmentId",
                table: "Messages",
                column: "FileAttachmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_FileAttachments_FileAttachmentId",
                table: "Messages",
                column: "FileAttachmentId",
                principalTable: "FileAttachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_FileAttachments_FileAttachmentId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_FileAttachmentId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "FileAttachmentId",
                table: "Messages");
        }
    }
}
