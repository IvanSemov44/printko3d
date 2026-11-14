using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOrderStatusCreatedIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_orders_status_created_at",
            table: "orders",
            columns: new[] { "status", "created_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_orders_status_created_at",
            table: "orders");
    }
}
