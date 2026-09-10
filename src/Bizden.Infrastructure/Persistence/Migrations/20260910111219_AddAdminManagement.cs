using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bizden.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BlockedAt",
                table: "host_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockedReason",
                table: "host_users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "host_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockedAt",
                table: "host_users");

            migrationBuilder.DropColumn(
                name: "BlockedReason",
                table: "host_users");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "host_users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "events");
        }
    }
}
