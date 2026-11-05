using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EarlyEagles.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_clerk_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.id);
                    table.ForeignKey(
                        name: "FK_announcements_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "announcement_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    announcement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_clerk_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcement_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_announcement_comments_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalTable: "announcements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_announcement_comments_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_announcement_comments_announcement_id",
                table: "announcement_comments",
                column: "announcement_id");

            migrationBuilder.CreateIndex(
                name: "IX_announcement_comments_tenant_id_announcement_id_created_at",
                table: "announcement_comments",
                columns: new[] { "tenant_id", "announcement_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_announcements_tenant_id_created_at",
                table: "announcements",
                columns: new[] { "tenant_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcement_comments");

            migrationBuilder.DropTable(
                name: "announcements");
        }
    }
}
