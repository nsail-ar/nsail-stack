using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NSail.Sample.Migrations
{
    /// <inheritdoc />
    public partial class TenantEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Entitlements",
                table: "TenantStates",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EntitlementsUpdatedAt",
                table: "TenantStates",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Entitlements",
                table: "TenantStates");

            migrationBuilder.DropColumn(
                name: "EntitlementsUpdatedAt",
                table: "TenantStates");
        }
    }
}
