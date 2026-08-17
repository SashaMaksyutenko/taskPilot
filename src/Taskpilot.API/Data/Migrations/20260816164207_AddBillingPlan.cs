using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Taskpilot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Plan",
                table: "OrganizationSettings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Free");

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanRenewsAt",
                table: "OrganizationSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "OrganizationSettings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "OrganizationSettings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "OrganizationSettings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-0000000005e7"),
                columns: new[] { "Plan", "PlanRenewsAt", "StripeCustomerId", "StripeSubscriptionId" },
                values: new object[] { "Free", null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Plan",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "PlanRenewsAt",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "OrganizationSettings");
        }
    }
}
