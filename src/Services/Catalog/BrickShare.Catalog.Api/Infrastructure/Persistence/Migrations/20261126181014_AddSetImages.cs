using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrickShare.Catalog.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSetImages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "set_images",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                set_id = table.Column<Guid>(type: "uuid", nullable: false),
                original_blob_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_set_images", x => x.id);
                table.ForeignKey(
                    name: "FK_set_images_lego_sets_set_id",
                    column: x => x.set_id,
                    principalTable: "lego_sets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_set_images_set_id",
            table: "set_images",
            column: "set_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "set_images");
    }
}