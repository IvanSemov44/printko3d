using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOrders : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "orders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                number = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                total_amount = table.Column<long>(type: "bigint", nullable: false),
                customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                shipping_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                shipping_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                shipping_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                shipping_country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                terms_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                terms_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_orders", x => x.id);
                table.CheckConstraint("ck_orders_currency", "char_length(currency) = 3");
                table.CheckConstraint("ck_orders_number", "number BETWEEN 10000000 AND 99999999");
                table.CheckConstraint("ck_orders_shipping_country", "char_length(shipping_country) = 2");
                table.CheckConstraint("ck_orders_status", "status IN ('New')");
                table.CheckConstraint("ck_orders_total_amount", "total_amount >= 0");
            });

        migrationBuilder.CreateTable(
            name: "order_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                variant_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                unit_price = table.Column<long>(type: "bigint", nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false),
                line_total = table.Column<long>(type: "bigint", nullable: false),
                fulfilment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                lead_time_days = table.Column<int>(type: "integer", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_items", x => x.id);
                table.CheckConstraint("ck_order_items_fulfilment_type", "fulfilment_type IN ('InStock', 'MadeToOrder')");
                table.CheckConstraint("ck_order_items_lead_time_positive", "lead_time_days IS NULL OR lead_time_days > 0");
                table.CheckConstraint("ck_order_items_line_total", "line_total = unit_price * quantity");
                table.CheckConstraint("ck_order_items_quantity", "quantity >= 1");
                table.CheckConstraint("ck_order_items_unit_price", "unit_price >= 0");
                table.ForeignKey(
                    name: "FK_order_items_orders_order_id",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_order_items_product_variants_variant_id",
                    column: x => x.variant_id,
                    principalTable: "product_variants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_order_items_order_id",
            table: "order_items",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "IX_order_items_variant_id",
            table: "order_items",
            column: "variant_id");

        migrationBuilder.CreateIndex(
            name: "IX_orders_created_at",
            table: "orders",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "IX_orders_number",
            table: "orders",
            column: "number",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "order_items");

        migrationBuilder.DropTable(
            name: "orders");
    }
}
