using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCatalog : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "categories",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                image_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_categories", x => x.id);
                table.ForeignKey(
                    name: "FK_categories_categories_parent_id",
                    column: x => x.parent_id,
                    principalTable: "categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "store_settings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                store_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                default_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                default_locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                contact_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                contact_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                logo_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                primary_color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                is_demo = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_store_settings", x => x.id);
                table.CheckConstraint("ck_store_settings_single_row", "id = '0195a000-0000-7000-8000-000000000001'");
            });

        migrationBuilder.CreateTable(
            name: "products",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                category_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                short_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "text", nullable: false),
                fulfilment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                lead_time_days = table.Column<int>(type: "integer", nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                seo_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                seo_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_products", x => x.id);
                table.CheckConstraint("ck_products_fulfilment_type", "fulfilment_type IN ('InStock', 'MadeToOrder')");
                table.CheckConstraint("ck_products_lead_time_for_made_to_order", "fulfilment_type <> 'MadeToOrder' OR lead_time_days IS NOT NULL");
                table.CheckConstraint("ck_products_status", "status IN ('Draft', 'Published', 'Archived')");
                table.ForeignKey(
                    name: "FK_products_categories_category_id",
                    column: x => x.category_id,
                    principalTable: "categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "product_variants",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                price = table.Column<long>(type: "bigint", nullable: false),
                compare_at_price = table.Column<long>(type: "bigint", nullable: true),
                stock_quantity = table.Column<int>(type: "integer", nullable: true),
                weight_grams = table.Column<int>(type: "integer", nullable: false),
                attributes = table.Column<string>(type: "jsonb", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_product_variants", x => x.id);
                table.CheckConstraint("ck_product_variants_compare_at_price", "compare_at_price IS NULL OR compare_at_price >= 0");
                table.CheckConstraint("ck_product_variants_price", "price >= 0");
                table.CheckConstraint("ck_product_variants_stock_quantity", "stock_quantity IS NULL OR stock_quantity >= 0");
                table.ForeignKey(
                    name: "FK_product_variants_products_product_id",
                    column: x => x.product_id,
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "product_images",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                alt_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                is_primary = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_product_images", x => x.id);
                table.ForeignKey(
                    name: "FK_product_images_product_variants_variant_id",
                    column: x => x.variant_id,
                    principalTable: "product_variants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_product_images_products_product_id",
                    column: x => x.product_id,
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_categories_parent_id",
            table: "categories",
            column: "parent_id");

        migrationBuilder.CreateIndex(
            name: "IX_categories_slug",
            table: "categories",
            column: "slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_product_images_variant_id",
            table: "product_images",
            column: "variant_id");

        migrationBuilder.CreateIndex(
            name: "ux_product_images_one_primary_per_product",
            table: "product_images",
            column: "product_id",
            unique: true,
            filter: "is_primary");

        migrationBuilder.CreateIndex(
            name: "IX_product_variants_product_id",
            table: "product_variants",
            column: "product_id");

        migrationBuilder.CreateIndex(
            name: "IX_product_variants_sku",
            table: "product_variants",
            column: "sku",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_products_category_id_status",
            table: "products",
            columns: new[] { "category_id", "status" });

        migrationBuilder.CreateIndex(
            name: "IX_products_slug",
            table: "products",
            column: "slug",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "product_images");

        migrationBuilder.DropTable(
            name: "store_settings");

        migrationBuilder.DropTable(
            name: "product_variants");

        migrationBuilder.DropTable(
            name: "products");

        migrationBuilder.DropTable(
            name: "categories");
    }
}
