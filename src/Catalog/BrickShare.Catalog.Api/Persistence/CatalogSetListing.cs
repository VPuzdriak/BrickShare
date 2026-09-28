using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Persistence;

/// <summary>
/// One row of the public catalog: a set, and the two numbers a customer browses by that no
/// table stores. Read-only, keyless, and computed by Postgres on every query — see the view in
/// the AddCatalogBrowse migration.
/// </summary>
public sealed class CatalogSetListing
{
    public required Guid Id { get; init; }

    public required string SetNumber { get; init; }

    public required string Name { get; init; }

    public required Guid ThemeId { get; init; }

    public required string ThemeName { get; init; }

    public required int Year { get; init; }

    public required int PieceCount { get; init; }

    public required int MinimumAge { get; init; }

    public required int MinimumRentalDays { get; init; }

    public required int AvailableCount { get; init; }

    /// <summary>Null when no copy is available. Never zero for "no price".</summary>
    public decimal? StartingPrice { get; init; }
}

public sealed class CatalogSetListingConfiguration : IEntityTypeConfiguration<CatalogSetListing>
{
    public void Configure(EntityTypeBuilder<CatalogSetListing> builder)
    {
        // A view, not a table: EF reads from it and never generates a CreateTable for it. The
        // migration owns its SQL.
        builder.ToView("catalog_set_listings");
        builder.HasNoKey();

        builder.Property(listing => listing.Id).HasColumnName("id");
        builder.Property(listing => listing.SetNumber).HasColumnName("set_number");
        builder.Property(listing => listing.Name).HasColumnName("name");
        builder.Property(listing => listing.ThemeId).HasColumnName("theme_id");
        builder.Property(listing => listing.ThemeName).HasColumnName("theme_name");
        builder.Property(listing => listing.Year).HasColumnName("year");
        builder.Property(listing => listing.PieceCount).HasColumnName("piece_count");
        builder.Property(listing => listing.MinimumAge).HasColumnName("minimum_age");
        builder.Property(listing => listing.MinimumRentalDays).HasColumnName("minimum_rental_days");
        builder.Property(listing => listing.AvailableCount).HasColumnName("available_count");

        // Plain decimal, not Money: this is a number to filter and sort on, not a value the
        // domain will do arithmetic with. The column type matches every other price column.
        builder.Property(listing => listing.StartingPrice)
            .HasColumnName("starting_price")
            .HasColumnType("numeric(10,2)");
    }
}
