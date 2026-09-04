using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bizden.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CoverImageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    EventDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadStartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UploadEndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.Id);
                    table.CheckConstraint("ck_events_upload_window", "\"UploadStartAt\" <= \"UploadEndAt\"");
                });

            migrationBuilder.CreateTable(
                name: "host_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_host_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UploadLimit = table.Column<int>(type: "integer", nullable: false),
                    ReservedUploads = table.Column<int>(type: "integer", nullable: false),
                    CompletedUploads = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitations", x => x.Id);
                    table.CheckConstraint("ck_invitations_limit_not_exceeded", "\"ReservedUploads\" + \"CompletedUploads\" <= \"UploadLimit\"");
                    table.CheckConstraint("ck_invitations_non_negative_counters", "\"ReservedUploads\" >= 0 AND \"CompletedUploads\" >= 0");
                    table.CheckConstraint("ck_invitations_positive_limit", "\"UploadLimit\" > 0");
                    table.ForeignKey(
                        name: "FK_invitations_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "photos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photos", x => x.Id);
                    table.CheckConstraint("ck_photos_positive_file_size", "\"FileSize\" > 0");
                    table.ForeignKey(
                        name: "FK_photos_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_photos_invitations_InvitationId",
                        column: x => x.InvitationId,
                        principalTable: "invitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "upload_reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upload_reservations", x => x.Id);
                    table.CheckConstraint("ck_upload_reservations_expiry", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_upload_reservations_invitations_InvitationId",
                        column: x => x.InvitationId,
                        principalTable: "invitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_upload_reservations_photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_events_OwnerId",
                table: "events",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_events_PublicId",
                table: "events",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_events_Slug",
                table: "events",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_host_users_NormalizedEmail",
                table: "host_users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invitations_EventId",
                table: "invitations",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_TokenHash",
                table: "invitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_photos_EventId_CreatedAt",
                table: "photos",
                columns: new[] { "EventId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_photos_InvitationId_CreatedAt",
                table: "photos",
                columns: new[] { "InvitationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_photos_StorageKey",
                table: "photos",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_upload_reservations_ExpiresAt",
                table: "upload_reservations",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_upload_reservations_InvitationId_IdempotencyKey",
                table: "upload_reservations",
                columns: new[] { "InvitationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_upload_reservations_InvitationId_Status",
                table: "upload_reservations",
                columns: new[] { "InvitationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_upload_reservations_PhotoId",
                table: "upload_reservations",
                column: "PhotoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "host_users");

            migrationBuilder.DropTable(
                name: "upload_reservations");

            migrationBuilder.DropTable(
                name: "photos");

            migrationBuilder.DropTable(
                name: "invitations");

            migrationBuilder.DropTable(
                name: "events");
        }
    }
}
