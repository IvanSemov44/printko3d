using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCarts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "carts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_carts", x => x.id);
                table.CheckConstraint("ck_carts_token_hash_length", "octet_length(token_hash) = 32");
            });

        migrationBuilder.CreateTable(
            name: "cart_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                cart_id = table.Column<Guid>(type: "uuid", nullable: false),
                variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cart_items", x => x.id);
                table.CheckConstraint("ck_cart_items_quantity", "quantity BETWEEN 1 AND 20");
                table.ForeignKey(
                    name: "FK_cart_items_carts_cart_id",
                    column: x => x.cart_id,
                    principalTable: "carts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_cart_items_product_variants_variant_id",
                    column: x => x.variant_id,
                    principalTable: "product_variants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_cart_items_cart_id_variant_id",
            table: "cart_items",
            columns: new[] { "cart_id", "variant_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_cart_items_variant_id",
            table: "cart_items",
            column: "variant_id");

        migrationBuilder.CreateIndex(
            name: "IX_carts_token_hash",
            table: "carts",
            column: "token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_carts_updated_at",
            table: "carts",
            column: "updated_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "cart_items");

        migrationBuilder.DropTable(
            name: "carts");
    }
}
