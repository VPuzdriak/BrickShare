using BrickShare.Catalog.Api.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Data.Configuration;

internal sealed class LegoThemeConfiguration : IEntityTypeConfiguration<LegoTheme> {
  public void Configure(EntityTypeBuilder<LegoTheme> builder) {
    builder.HasKey(t => t.Id);
    builder.Property(t => t.Name).IsRequired().HasMaxLength(255);
    builder.HasIndex(t => t.Name).IsUnique();
  }
}
