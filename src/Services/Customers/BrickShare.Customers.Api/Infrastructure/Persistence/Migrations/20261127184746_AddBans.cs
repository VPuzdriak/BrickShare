using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrickShare.Customers.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBans : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "status",
            table: "customers",
            type: "text",
            nullable: false,
            defaultValueSql: "'Active'");

        migrationBuilder.CreateTable(
            name: "customer_bans",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                reason = table.Column<string>(type: "text", nullable: false),
                comment = table.Column<string>(type: "text", nullable: false),
                banned_by = table.Column<string>(type: "text", nullable: false),
                banned_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                banned_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                lifted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_customer_bans", x => x.id);
                table.ForeignKey(
                    name: "FK_customer_bans_customers_customer_id",
                    column: x => x.customer_id,
                    principalTable: "customers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_customer_bans_customer_id",
            table: "customer_bans",
            column: "customer_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "customer_bans");

        migrationBuilder.DropColumn(
            name: "status",
            table: "customers");
    }
}