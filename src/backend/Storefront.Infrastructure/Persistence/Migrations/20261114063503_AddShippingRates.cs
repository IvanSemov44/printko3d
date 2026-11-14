using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddShippingRates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "shipping_rates",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                method_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                max_weight_grams = table.Column<int>(type: "integer", nullable: false),
                price = table.Column<long>(type: "bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_shipping_rates", x => x.id);
                table.CheckConstraint("ck_shipping_rates_max_weight_grams", "max_weight_grams > 0");
                table.CheckConstraint("ck_shipping_rates_price", "price >= 0");
            });

        migrationBuilder.CreateIndex(
            name: "IX_shipping_rates_method_code_max_weight_grams",
            table: "shipping_rates",
            columns: new[] { "method_code", "max_weight_grams" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "shipping_rates");
    }
}
