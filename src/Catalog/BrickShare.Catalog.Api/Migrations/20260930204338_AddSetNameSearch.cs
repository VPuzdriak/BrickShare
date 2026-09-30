using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrickShare.Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSetNameSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_sets_name_trgm",
                table: "catalog_sets",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_catalog_sets_name_trgm",
                table: "catalog_sets");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
