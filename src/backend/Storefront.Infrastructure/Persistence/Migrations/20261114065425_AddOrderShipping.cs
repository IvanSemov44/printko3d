using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOrderShipping : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "shipping_amount",
            table: "orders",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<string>(
            name: "shipping_method",
            table: "orders",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_shipping_amount",
            table: "orders",
            sql: "shipping_amount >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_total_covers_shipping",
            table: "orders",
            sql: "total_amount >= shipping_amount");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_shipping_amount",
            table: "orders");

        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_total_covers_shipping",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "shipping_amount",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "shipping_method",
            table: "orders");
    }
}
