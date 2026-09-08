using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bizden.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHostEmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationCodeHash",
                table: "host_users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerificationExpiresAt",
                table: "host_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerificationSentAt",
                table: "host_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerifiedAt",
                table: "host_users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailVerificationCodeHash",
                table: "host_users");

            migrationBuilder.DropColumn(
                name: "EmailVerificationExpiresAt",
                table: "host_users");

            migrationBuilder.DropColumn(
                name: "EmailVerificationSentAt",
                table: "host_users");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAt",
                table: "host_users");
        }
    }
}
