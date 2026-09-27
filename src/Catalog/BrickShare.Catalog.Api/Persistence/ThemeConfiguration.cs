using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Persistence;

public sealed class ThemeConfiguration : IEntityTypeConfiguration<Theme>
{
    public void Configure(EntityTypeBuilder<Theme> builder)
    {
        builder.ToTable("themes");

        builder.HasKey(theme => theme.Id);
        builder.Property(theme => theme.Id).HasColumnName("id");

        builder.Property(theme => theme.RebrickableId).HasColumnName("rebrickable_id").IsRequired();

        builder.HasIndex(theme => theme.RebrickableId)
            .IsUnique()
            .HasDatabaseName("ix_themes_rebrickable_id");

        builder.Property(theme => theme.Name)
            .HasColumnName("name")
            .HasMaxLength(Theme.MaxNameLength)
            .IsRequired();

        builder.HasIndex(theme => theme.Name)
            .HasDatabaseName("ix_themes_name");
    }
}
