using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrickShare.Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    set_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    theme = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    piece_count = table.Column<int>(type: "integer", nullable: false),
                    retail_price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    base_rental_price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    minimum_rental_days = table.Column<int>(type: "integer", nullable: false),
                    minimum_age = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_sets", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_catalog_sets_set_number",
                table: "catalog_sets",
                column: "set_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_sets");
        }
    }
}
