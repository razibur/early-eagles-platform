using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EarlyEagles.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantClerkOrganizationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "clerk_organization_id",
                table: "tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_clerk_organization_id",
                table: "tenants",
                column: "clerk_organization_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tenants_clerk_organization_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "clerk_organization_id",
                table: "tenants");
        }
    }
}
