using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOutgoingEmails : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "outgoing_emails",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                to_address = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                text_body = table.Column<string>(type: "text", nullable: false),
                html_body = table.Column<string>(type: "text", nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false),
                next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outgoing_emails", x => x.id);
                table.CheckConstraint("ck_outgoing_emails_attempts", "attempts >= 0");
                table.CheckConstraint("ck_outgoing_emails_status", "status IN ('Pending', 'Sent', 'Failed')");
            });

        migrationBuilder.CreateIndex(
            name: "IX_outgoing_emails_status_next_attempt_at",
            table: "outgoing_emails",
            columns: new[] { "status", "next_attempt_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "outgoing_emails");
    }
}
