using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
/// <remarks>
/// StoreSettings.Version is mapped to xmin, a system column that PostgreSQL adds to every table itself.
/// EF generated an AddColumn for it, which PostgreSQL would reject (the name is reserved), so Up and Down
/// are empty on purpose, as in CategoryRowVersion. Only the model snapshot changes.
/// </remarks>
public partial class StoreSettingsRowVersion : Migration
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
