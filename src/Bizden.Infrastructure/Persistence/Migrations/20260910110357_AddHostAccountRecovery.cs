using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bizden.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHostAccountRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailChangeCodeExpiresAt",
                table: "host_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailChangeCodeHash",
                table: "host_users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailChangeCodeSentAt",
                table: "host_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PasswordResetCodeExpiresAt",
                table: "host_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetCodeHash",
                table: "host_users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PasswordResetCodeSentAt",
                table: "host_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingEmail",
                table: "host_users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingNormalizedEmail",
                table: "host_users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailChangeCodeExpiresAt",
                table: "host_users");

            migrationBuilder.DropColumn(
                name: "EmailChangeCodeHash",
                table: "host_users");

            migrationBuilder.DropColumn(
                name: "EmailChangeCodeSentAt",
                table: "host_users");

            migrationBuilder.DropColumn(
                name: "PasswordResetCodeExpiresAt",
                table: "host_users");

            migrationBuilder.DropColumn(
                name: "PasswordResetCodeHash",
                table: "host_users");

            migrationBuilder.DropColumn(
                name: "PasswordResetCodeSentAt",
                table: "host_users");

            migrationBuilder.DropColumn(
                name: "PendingEmail",
                table: "host_users");

            migrationBuilder.DropColumn(
                name: "PendingNormalizedEmail",
                table: "host_users");
        }
    }
}
