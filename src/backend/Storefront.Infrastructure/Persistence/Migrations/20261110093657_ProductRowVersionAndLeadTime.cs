using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
/// <remarks>
/// Product.Version is mapped to xmin, a system column that PostgreSQL adds to every table itself.
/// EF generated an AddColumn for it, which PostgreSQL would reject (the name is reserved), so the column is left out
/// on purpose (as in CategoryRowVersion). The migration only adds the check that a lead time is a positive number.
/// </remarks>
public partial class ProductRowVersionAndLeadTime : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddCheckConstraint(
            name: "ck_products_lead_time_positive",
            table: "products",
            sql: "lead_time_days IS NULL OR lead_time_days > 0");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_products_lead_time_positive",
            table: "products");
    }
}
