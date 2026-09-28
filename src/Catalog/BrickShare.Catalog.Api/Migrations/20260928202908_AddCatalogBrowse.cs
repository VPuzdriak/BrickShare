using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BrickShare.Catalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogBrowse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grade_multipliers",
                columns: table => new
                {
                    grade = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    multiplier = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grade_multipliers", x => x.grade);
                    table.CheckConstraint("ck_grade_multipliers_positive", "multiplier > 0");
                });

            migrationBuilder.InsertData(
                table: "grade_multipliers",
                columns: new[] { "grade", "multiplier" },
                values: new object[,]
                {
                    { "Excellent", 0.85m },
                    { "Fair", 0.55m },
                    { "Good", 0.70m },
                    { "New", 1.00m }
                });

            // Hand-written: EF maps the view but never generates one
            migrationBuilder.Sql(
                """
                create view catalog_set_listings as
                select
                    s.id,
                    s.set_number,
                    s.name,
                    s.theme_id,
                    t.name as theme_name,
                    s.year,
                    s.piece_count,
                    s.minimum_age,
                    s.minimum_rental_days,
                    (count(c.id) filter (where c.status = 'Available'))::int as available_count,
                    round(
                        s.base_rental_price * min(m.multiplier) filter (where c.status = 'Available'),
                        2) as starting_price
                from catalog_sets s
                join themes t on t.id = s.theme_id
                left join copies c on c.catalog_set_id = s.id
                left join grade_multipliers m on m.grade = c.grade
                group by s.id, t.id;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("drop view catalog_set_listings;");

            migrationBuilder.DropTable(
                name: "grade_multipliers");
        }
    }
}
