using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EarlyEagles.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    actor_clerk_user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_events_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_tenant_id_occurred_at",
                table: "audit_events",
                columns: new[] { "tenant_id", "occurred_at" },
                descending: new[] { false, true });

            // P0-046.2 — the storage-layer backstop for the append-only contract.
            // The AuditEvent aggregate has no updater methods, but ExecuteUpdate /
            // ExecuteDelete bypass the change tracker and thus the write-side
            // TenantStampingInterceptor; a hand-rolled SQL script would bypass
            // both. This trigger refuses any UPDATE, DELETE, or TRUNCATE at the
            // database level so a compromised or misbehaving caller cannot rewrite
            // history. RAISE EXCEPTION surfaces to Npgsql as a PostgresException
            // (SQLSTATE P0001), which is what the integration tests assert on.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION audit_events_reject_change()
                RETURNS TRIGGER
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_events is append-only: % is not allowed', TG_OP
                        USING ERRCODE = 'P0001';
                END;
                $$;

                CREATE TRIGGER audit_events_reject_update_delete
                BEFORE UPDATE OR DELETE ON audit_events
                FOR EACH ROW EXECUTE FUNCTION audit_events_reject_change();

                CREATE TRIGGER audit_events_reject_truncate
                BEFORE TRUNCATE ON audit_events
                FOR EACH STATEMENT EXECUTE FUNCTION audit_events_reject_change();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the triggers before the table so the DROP TABLE isn't itself
            // rejected. The function is dropped last since both triggers depend on it.
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS audit_events_reject_truncate ON audit_events;
                DROP TRIGGER IF EXISTS audit_events_reject_update_delete ON audit_events;
            ");

            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS audit_events_reject_change();");
        }
    }
}
