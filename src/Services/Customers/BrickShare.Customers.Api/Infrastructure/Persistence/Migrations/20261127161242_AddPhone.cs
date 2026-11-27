using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrickShare.Customers.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPhone : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "phone",
            table: "customers",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "phone",
            table: "customers");
    }
}