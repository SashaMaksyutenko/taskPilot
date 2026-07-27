using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskpilot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_MarketplaceTaskId_RaterId",
                table: "Reviews");

            migrationBuilder.AlterColumn<Guid>(
                name: "MarketplaceTaskId",
                table: "Reviews",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // Existing rows are all marketplace reviews — default the new column accordingly.
            migrationBuilder.AddColumn<string>(
                name: "Context",
                table: "Reviews",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Marketplace");

            migrationBuilder.AddColumn<Guid>(
                name: "ContextId",
                table: "Reviews",
                type: "uuid",
                nullable: true);

            // Backfill the context id of existing marketplace reviews from their task id so the
            // new per-context unique index below is consistent with the old (task, rater) one.
            migrationBuilder.Sql(
                "UPDATE \"Reviews\" SET \"ContextId\" = \"MarketplaceTaskId\" " +
                "WHERE \"ContextId\" IS NULL AND \"MarketplaceTaskId\" IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Context_ContextId_RaterId_RateeId",
                table: "Reviews",
                columns: new[] { "Context", "ContextId", "RaterId", "RateeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_MarketplaceTaskId",
                table: "Reviews",
                column: "MarketplaceTaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_Context_ContextId_RaterId_RateeId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_MarketplaceTaskId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "Context",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ContextId",
                table: "Reviews");

            migrationBuilder.AlterColumn<Guid>(
                name: "MarketplaceTaskId",
                table: "Reviews",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_MarketplaceTaskId_RaterId",
                table: "Reviews",
                columns: new[] { "MarketplaceTaskId", "RaterId" },
                unique: true);
        }
    }
}
