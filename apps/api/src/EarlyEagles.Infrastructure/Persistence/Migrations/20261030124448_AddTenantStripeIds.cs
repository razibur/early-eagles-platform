using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EarlyEagles.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantStripeIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "stripe_customer_id",
                table: "tenants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_subscription_id",
                table: "tenants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_stripe_customer_id",
                table: "tenants",
                column: "stripe_customer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_stripe_subscription_id",
                table: "tenants",
                column: "stripe_subscription_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tenants_stripe_customer_id",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenants_stripe_subscription_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "stripe_customer_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "stripe_subscription_id",
                table: "tenants");
        }
    }
}
