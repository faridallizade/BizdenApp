using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bizden.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoStorageDeletionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StorageDeletedAt",
                table: "photos",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StorageDeletedAt",
                table: "photos");
        }
    }
}
