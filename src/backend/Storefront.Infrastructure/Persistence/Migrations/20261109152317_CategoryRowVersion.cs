using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
/// <remarks>
/// Category.Version is mapped to xmin, a system column that PostgreSQL adds to every table itself.
/// EF generated an AddColumn for it, which PostgreSQL would reject (the name is reserved), so Up and Down
/// are empty on purpose. Only the model snapshot changes.
/// </remarks>
public partial class CategoryRowVersion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
