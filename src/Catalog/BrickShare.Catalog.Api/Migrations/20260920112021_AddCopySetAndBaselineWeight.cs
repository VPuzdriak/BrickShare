using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrickShare.Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCopySetAndBaselineWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "baseline_weight_grams",
                table: "copies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "catalog_set_id",
                table: "copies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_copies_catalog_set_id",
                table: "copies",
                column: "catalog_set_id");

            migrationBuilder.AddForeignKey(
                name: "fk_copies_catalog_set_id",
                table: "copies",
                column: "catalog_set_id",
                principalTable: "catalog_sets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_copies_catalog_set_id",
                table: "copies");

            migrationBuilder.DropIndex(
                name: "ix_copies_catalog_set_id",
                table: "copies");

            migrationBuilder.DropColumn(
                name: "baseline_weight_grams",
                table: "copies");

            migrationBuilder.DropColumn(
                name: "catalog_set_id",
                table: "copies");
        }
    }
}
