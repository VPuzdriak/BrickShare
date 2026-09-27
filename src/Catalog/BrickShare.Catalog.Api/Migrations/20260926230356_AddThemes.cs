using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrickShare.Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddThemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "themes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rebrickable_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_themes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_themes_rebrickable_id",
                table: "themes",
                column: "rebrickable_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_themes_name",
                table: "themes",
                column: "name");

            migrationBuilder.Sql(
                """
                insert into themes (id, rebrickable_id, name)
                select distinct on (theme_id) uuidv7(), theme_id, theme_name
                from rebrickable_snapshots
                order by theme_id, fetched_at desc;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "theme_id",
                table: "catalog_sets",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                update catalog_sets s
                set theme_id = t.id
                from (
                    select distinct on (set_number) set_number, theme_id
                    from rebrickable_snapshots
                    order by set_number, fetched_at desc
                ) latest
                join themes t on t.rebrickable_id = latest.theme_id
                where s.set_number = latest.set_number;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "theme_id",
                table: "catalog_sets",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_catalog_sets_theme_id",
                table: "catalog_sets",
                column: "theme_id");

            migrationBuilder.AddForeignKey(
                name: "fk_catalog_sets_theme_id",
                table: "catalog_sets",
                column: "theme_id",
                principalTable: "themes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "theme",
                table: "catalog_sets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "theme",
                table: "catalog_sets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // A Down that drops the foreign key and leaves the names behind is not a Down.
            // Going back has to restore the data too, or it is a data-loss button with a
            // reassuring name on it.
            migrationBuilder.Sql(
                """
                update catalog_sets s
                set theme = t.name
                from themes t
                where t.id = s.theme_id;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "theme",
                table: "catalog_sets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "fk_catalog_sets_theme_id",
                table: "catalog_sets");

            migrationBuilder.DropIndex(
                name: "ix_catalog_sets_theme_id",
                table: "catalog_sets");

            migrationBuilder.DropColumn(
                name: "theme_id",
                table: "catalog_sets");

            migrationBuilder.DropTable(
                name: "themes");
        }
    }
}
