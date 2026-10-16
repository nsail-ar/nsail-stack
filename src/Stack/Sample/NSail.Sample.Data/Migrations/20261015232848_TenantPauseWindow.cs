// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NSail.Sample.Migrations
{
    /// <inheritdoc />
    public partial class TenantPauseWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "TenantStates",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "TenantStates",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                collation: "case_insensitive");

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedFrom",
                table: "TenantStates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedUntil",
                table: "TenantStates",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Message",
                table: "TenantStates");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "TenantStates");

            migrationBuilder.DropColumn(
                name: "SuspendedFrom",
                table: "TenantStates");

            migrationBuilder.DropColumn(
                name: "SuspendedUntil",
                table: "TenantStates");
        }
    }
}
