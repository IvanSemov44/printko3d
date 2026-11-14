using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPayments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_status",
            table: "orders");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "payment_due_at",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "payments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                provider_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                public_id = table.Column<Guid>(type: "uuid", nullable: false),
                amount = table.Column<long>(type: "bigint", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payments", x => x.id);
                table.CheckConstraint("ck_payments_amount", "amount > 0");
                table.CheckConstraint("ck_payments_currency", "char_length(currency) = 3");
                table.CheckConstraint("ck_payments_status", "status IN ('Pending', 'Succeeded', 'Failed', 'PaidWithoutOrder')");
                table.ForeignKey(
                    name: "FK_payments_orders_order_id",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "payment_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                event_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payment_events", x => x.id);
                table.ForeignKey(
                    name: "FK_payment_events_payments_payment_id",
                    column: x => x.payment_id,
                    principalTable: "payments",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_status",
            table: "orders",
            sql: "status IN ('New', 'Paid', 'Cancelled')");

        migrationBuilder.CreateIndex(
            name: "IX_payment_events_payment_id",
            table: "payment_events",
            column: "payment_id");

        migrationBuilder.CreateIndex(
            name: "IX_payment_events_provider_event_id",
            table: "payment_events",
            columns: new[] { "provider", "event_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_payments_provider_provider_reference",
            table: "payments",
            columns: new[] { "provider", "provider_reference" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_payments_public_id",
            table: "payments",
            column: "public_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_payments_one_open_payment_per_order",
            table: "payments",
            column: "order_id",
            unique: true,
            filter: "status IN ('Pending', 'Succeeded', 'PaidWithoutOrder')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "payment_events");

        migrationBuilder.DropTable(
            name: "payments");

        migrationBuilder.DropCheckConstraint(
            name: "ck_orders_status",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "payment_due_at",
            table: "orders");

        migrationBuilder.AddCheckConstraint(
            name: "ck_orders_status",
            table: "orders",
            sql: "status IN ('New')");
    }
}
