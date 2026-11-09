// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NSail.Sample.Migrations
{
    /// <inheritdoc />
    public partial class TenantConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContactAddresses_Contacts_ContactId",
                table: "ContactAddresses");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Contacts_TenantId_Id",
                table: "Contacts",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_ContactAddresses_Contacts_TenantId_ContactId",
                table: "ContactAddresses",
                columns: new[] { "TenantId", "ContactId" },
                principalTable: "Contacts",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContactAddresses_Contacts_TenantId_ContactId",
                table: "ContactAddresses");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Contacts_TenantId_Id",
                table: "Contacts");

            migrationBuilder.AddForeignKey(
                name: "FK_ContactAddresses_Contacts_ContactId",
                table: "ContactAddresses",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
