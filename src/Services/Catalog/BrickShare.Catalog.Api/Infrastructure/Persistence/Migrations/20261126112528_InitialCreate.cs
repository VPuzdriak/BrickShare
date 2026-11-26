using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrickShare.Catalog.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lego_sets",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                lego_set_number = table.Column<string>(type: "text", nullable: false),
                min_rental_days = table.Column<int>(type: "integer", nullable: false),
                daily_price_amount = table.Column<long>(type: "bigint", nullable: false),
                daily_price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                deposit_amount = table.Column<long>(type: "bigint", nullable: false),
                deposit_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                late_fee_per_day_amount = table.Column<long>(type: "bigint", nullable: false),
                late_fee_per_day_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                replacement_value_amount = table.Column<long>(type: "bigint", nullable: false),
                replacement_value_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lego_sets", x => x.id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "lego_sets");
    }
}