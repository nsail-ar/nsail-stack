// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore.Migrations;
using NSail.Data;

#nullable disable

namespace NSail.Sample.Migrations
{
    /// <inheritdoc />
    public partial class TenantPolicies : Migration
    {
        // Every table the tenant pass puts the column on, and no other: TenantStates names the
        // tenant it is ABOUT in a column of its own and is install-scoped, so it owes none. The
        // list belongs to the chain and cannot be read off a snapshot, whose entity types carry
        // no CLR type to ask the marker of; what keeps it true is the sweep that reads the
        // migrated database back against the real model.
        static readonly string[] Walled = ["AppliedSeedSteps", "ContactAddresses", "Contacts"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddTenantPolicies(Walled);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTenantPolicies(Walled);
        }
    }
}
