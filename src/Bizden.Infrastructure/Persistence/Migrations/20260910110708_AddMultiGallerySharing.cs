using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bizden.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiGallerySharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shared_galleries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PinHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    EnabledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_galleries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shared_galleries_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shared_gallery_photos",
                columns: table => new
                {
                    SharedGalleryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_gallery_photos", x => new { x.SharedGalleryId, x.PhotoId });
                    table.ForeignKey(
                        name: "FK_shared_gallery_photos_photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shared_gallery_photos_shared_galleries_SharedGalleryId",
                        column: x => x.SharedGalleryId,
                        principalTable: "shared_galleries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shared_galleries_EventId_DeletedAt",
                table: "shared_galleries",
                columns: new[] { "EventId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_shared_galleries_PublicId",
                table: "shared_galleries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shared_gallery_photos_PhotoId",
                table: "shared_gallery_photos",
                column: "PhotoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shared_gallery_photos");

            migrationBuilder.DropTable(
                name: "shared_galleries");
        }
    }
}
