using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrickShare.Catalog.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSetInstances : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "set_instances",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                set_id = table.Column<Guid>(type: "uuid", nullable: false),
                inventory_code = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                condition = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_set_instances", x => x.id);
                table.ForeignKey(
                    name: "FK_set_instances_lego_sets_set_id",
                    column: x => x.set_id,
                    principalTable: "lego_sets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_set_instances_inventory_code",
            table: "set_instances",
            column: "inventory_code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_set_instances_set_id",
            table: "set_instances",
            column: "set_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "set_instances");
    }
}