using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Infrastructure.Persistence;

public sealed class ThemeConfiguration : IEntityTypeConfiguration<Theme>
{
    public void Configure(EntityTypeBuilder<Theme> builder)
    {
        builder.ToTable("themes");

        builder.HasKey(theme => theme.Id);

        builder.Property(theme => theme.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(theme => theme.Name)
            .HasColumnName("name");

        builder.Property(theme => theme.RebrickableThemeId)
            .HasColumnName("rebrickable_theme_id");

        builder.HasIndex(theme => theme.RebrickableThemeId)
            .IsUnique();

        builder.Ignore(theme => theme.DomainEvents);
    }
}