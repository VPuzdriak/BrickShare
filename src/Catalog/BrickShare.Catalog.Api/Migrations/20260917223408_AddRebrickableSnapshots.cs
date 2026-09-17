using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrickShare.Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRebrickableSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rebrickable_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    set_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    theme_id = table.Column<int>(type: "integer", nullable: false),
                    theme_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    piece_count = table.Column<int>(type: "integer", nullable: false),
                    image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rebrickable_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rebrickable_snapshots_set_number",
                table: "rebrickable_snapshots",
                column: "set_number");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rebrickable_snapshots");
        }
    }
}
