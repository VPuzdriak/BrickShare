using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrickShare.Catalog.Api.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRebrickableSpecsAndThemes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "minifigure_count",
            table: "lego_sets",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "piece_count",
            table: "lego_sets",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "rebrickable_set_num",
            table: "lego_sets",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "specs_fetched_at",
            table: "lego_sets",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "theme_id",
            table: "lego_sets",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "year",
            table: "lego_sets",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "themes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                rebrickable_theme_id = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_themes", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_lego_sets_rebrickable_set_num",
            table: "lego_sets",
            column: "rebrickable_set_num",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_themes_rebrickable_theme_id",
            table: "themes",
            column: "rebrickable_theme_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "themes");

        migrationBuilder.DropIndex(
            name: "IX_lego_sets_rebrickable_set_num",
            table: "lego_sets");

        migrationBuilder.DropColumn(
            name: "minifigure_count",
            table: "lego_sets");

        migrationBuilder.DropColumn(
            name: "piece_count",
            table: "lego_sets");

        migrationBuilder.DropColumn(
            name: "rebrickable_set_num",
            table: "lego_sets");

        migrationBuilder.DropColumn(
            name: "specs_fetched_at",
            table: "lego_sets");

        migrationBuilder.DropColumn(
            name: "theme_id",
            table: "lego_sets");

        migrationBuilder.DropColumn(
            name: "year",
            table: "lego_sets");
    }
}