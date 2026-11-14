using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCustomerAccounts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "customer_id",
            table: "orders",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "customer_id",
            table: "carts",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "customer_users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                normalized_user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                normalized_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: true),
                security_stamp = table.Column<string>(type: "text", nullable: true),
                concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                phone_number = table.Column<string>(type: "text", nullable: true),
                phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                access_failed_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_customer_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "customer_external_logins",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                provider_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_customer_external_logins", x => x.id);
                table.ForeignKey(
                    name: "FK_customer_external_logins_customer_users_customer_id",
                    column: x => x.customer_id,
                    principalTable: "customer_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_orders_customer_id",
            table: "orders",
            column: "customer_id");

        migrationBuilder.CreateIndex(
            name: "IX_carts_customer_id",
            table: "carts",
            column: "customer_id",
            unique: true,
            filter: "customer_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_customer_external_logins_customer_id",
            table: "customer_external_logins",
            column: "customer_id");

        migrationBuilder.CreateIndex(
            name: "IX_customer_external_logins_provider_provider_key",
            table: "customer_external_logins",
            columns: new[] { "provider", "provider_key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_customer_users_normalized_email",
            table: "customer_users",
            column: "normalized_email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_customer_users_normalized_user_name",
            table: "customer_users",
            column: "normalized_user_name",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_carts_customer_users_customer_id",
            table: "carts",
            column: "customer_id",
            principalTable: "customer_users",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_orders_customer_users_customer_id",
            table: "orders",
            column: "customer_id",
            principalTable: "customer_users",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_carts_customer_users_customer_id",
            table: "carts");

        migrationBuilder.DropForeignKey(
            name: "FK_orders_customer_users_customer_id",
            table: "orders");

        migrationBuilder.DropTable(
            name: "customer_external_logins");

        migrationBuilder.DropTable(
            name: "customer_users");

        migrationBuilder.DropIndex(
            name: "IX_orders_customer_id",
            table: "orders");

        migrationBuilder.DropIndex(
            name: "IX_carts_customer_id",
            table: "carts");

        migrationBuilder.DropColumn(
            name: "customer_id",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "customer_id",
            table: "carts");
    }
}
