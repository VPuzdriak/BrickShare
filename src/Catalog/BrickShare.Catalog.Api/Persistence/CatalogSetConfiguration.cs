using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Persistence;

public sealed class CatalogSetConfiguration : IEntityTypeConfiguration<CatalogSet>
{
    public void Configure(EntityTypeBuilder<CatalogSet> builder)
    {
        builder.ToTable("catalog_sets");

        builder.HasKey(set => set.Id);
        builder.Property(set => set.Id).HasColumnName("id");

        builder.Property(set => set.Number)
            .HasColumnName("set_number")
            .HasConversion(number => number.Value, value => SetNumber.Parse(value))
            .HasMaxLength(32)
            .IsRequired();

        // The invariant, in the database. Episode 16 made the same argument for label codes:
        // an application check is a race, and a unique index is an arbiter. Episode 24 uses it.
        builder.HasIndex(set => set.Number)
            .IsUnique()
            .HasDatabaseName("ix_catalog_sets_set_number");

        builder.Property(set => set.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(set => set.Theme)
            .HasColumnName("theme")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(set => set.Year).HasColumnName("year").IsRequired();
        builder.Property(set => set.PieceCount).HasColumnName("piece_count").IsRequired();

        // No HasColumnType on the two money columns. CatalogDbContext.ConfigureConventions
        // already says every Money in this model is numeric(10,2), and episode 16 put it there
        // precisely so this file cannot get it wrong by forgetting.
        builder.Property(set => set.RetailPrice).HasColumnName("retail_price").IsRequired();
        builder.Property(set => set.BaseRentalPrice).HasColumnName("base_rental_price").IsRequired();

        builder.Property(set => set.MinimumRentalDays).HasColumnName("minimum_rental_days").IsRequired();
        builder.Property(set => set.MinimumAge).HasColumnName("minimum_age").IsRequired();

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();
    }
}
