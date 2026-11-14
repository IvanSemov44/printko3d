using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOrderStatuses : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_status",
            table: "orders");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_status",
            table: "orders",
            sql: "status IN ('New', 'Paid', 'InProduction', 'ReadyToShip', 'Shipped', 'Completed', 'Cancelled')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_status",
            table: "orders");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_status",
            table: "orders",
            sql: "status IN ('New', 'Paid', 'Cancelled')");
    }
}
