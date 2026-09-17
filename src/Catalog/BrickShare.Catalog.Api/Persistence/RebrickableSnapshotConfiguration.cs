using BrickShare.Catalog.Api.Rebrickable;
using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BrickShare.Catalog.Api.Persistence;

public sealed class RebrickableSnapshotConfiguration : IEntityTypeConfiguration<RebrickableSnapshot>
{
    public void Configure(EntityTypeBuilder<RebrickableSnapshot> builder)
    {
        builder.ToTable("rebrickable_snapshots");

        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Id).HasColumnName("id");

        builder.Property(snapshot => snapshot.Number)
            .HasColumnName("set_number")
            .HasConversion(number => number.Value, value => SetNumber.Parse(value))
            .HasMaxLength(SetNumber.MaxLength)
            .IsRequired();

        // No unique index, and this is the one line in the file worth arguing about. Looking the
        // same set up twice is normal — two staff members, or one who closed the tab — and each
        // lookup is a separate fact with its own fetched_at. The invariant "one catalog_set per
        // set number" is already enforced one table over.
        builder.HasIndex(snapshot => snapshot.Number)
            .HasDatabaseName("ix_rebrickable_snapshots_set_number");

        builder.Property(snapshot => snapshot.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(snapshot => snapshot.ThemeId).HasColumnName("theme_id").IsRequired();

        builder.Property(snapshot => snapshot.ThemeName)
            .HasColumnName("theme_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(snapshot => snapshot.Year).HasColumnName("year").IsRequired();
        builder.Property(snapshot => snapshot.PieceCount).HasColumnName("piece_count").IsRequired();

        // EF would convert a Uri by convention. Naming the converter makes the column shape a
        // decision rather than a default somebody has to go and look up.
        builder.Property(snapshot => snapshot.ImageUrl)
            .HasColumnName("image_url")
            .HasConversion(new UriToStringConverter())
            .HasMaxLength(2048);

        builder.Property(snapshot => snapshot.FetchedAt)
            .HasColumnName("fetched_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
