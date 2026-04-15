using BrickShare.Catalog.Api.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Data.Configuration;

internal sealed class LegoSetConfiguration : IEntityTypeConfiguration<LegoSet> {
  public void Configure(EntityTypeBuilder<LegoSet> builder) {
    builder.HasKey(s => s.Id);
    builder.Property(s => s.Name).IsRequired().HasMaxLength(255);
    builder.Property(s => s.LegoId).IsRequired().HasMaxLength(10);
    builder.HasIndex(s => s.LegoId).IsUnique();
    builder.Property(s => s.ReleaseDate).IsRequired();
    builder.Property(s => s.NumberOfParts).IsRequired();
    builder.Property(s => s.AgeFrom).IsRequired();

    builder.HasOne(s => s.Theme)
      .WithMany()
      .HasForeignKey(s => s.ThemeId)
      .IsRequired();

    var iconsThemeId = new Guid("00000000-0000-0000-0000-000000000066");
    var technicThemeId = new Guid("00000000-0000-0000-0000-000000000128");

    builder.HasData([
      // Icons
      new LegoSet {
        Id = new Guid("00000000-0000-0000-0001-000000000001"),
        Name = "Eiffel Tower",
        LegoId = "10307",
        ReleaseDate = new DateOnly(2022, 11, 25),
        NumberOfParts = 10001,
        AgeFrom = 18,
        ThemeId = iconsThemeId,
        Theme = null!
      },
      new LegoSet {
        Id = new Guid("00000000-0000-0000-0001-000000000002"),
        Name = "Boutique Hotel",
        LegoId = "10297",
        ReleaseDate = new DateOnly(2022, 6, 1),
        NumberOfParts = 3066,
        AgeFrom = 18,
        ThemeId = iconsThemeId,
        Theme = null!
      },
      new LegoSet {
        Id = new Guid("00000000-0000-0000-0001-000000000003"),
        Name = "Land Rover Classic Defender 90",
        LegoId = "10317",
        ReleaseDate = new DateOnly(2023, 6, 1),
        NumberOfParts = 2336,
        AgeFrom = 18,
        ThemeId = iconsThemeId,
        Theme = null!
      },
      new LegoSet {
        Id = new Guid("00000000-0000-0000-0001-000000000004"),
        Name = "PAC-MAN Arcade",
        LegoId = "10323",
        ReleaseDate = new DateOnly(2023, 6, 1),
        NumberOfParts = 2651,
        AgeFrom = 18,
        ThemeId = iconsThemeId,
        Theme = null!
      },
      new LegoSet {
        Id = new Guid("00000000-0000-0000-0001-000000000005"),
        Name = "Atari 2600",
        LegoId = "10306",
        ReleaseDate = new DateOnly(2022, 8, 1),
        NumberOfParts = 2532,
        AgeFrom = 18,
        ThemeId = iconsThemeId,
        Theme = null!
      },
      // Technic
      new LegoSet {
        Id = new Guid("00000000-0000-0000-0001-000000000006"),
        Name = "Ferrari Daytona SP3",
        LegoId = "42143",
        ReleaseDate = new DateOnly(2022, 6, 1),
        NumberOfParts = 3778,
        AgeFrom = 18,
        ThemeId = technicThemeId,
        Theme = null!
      },
      new LegoSet {
        Id = new Guid("00000000-0000-0000-0001-000000000007"),
        Name = "PEUGEOT 9X8 24H Le Mans Hybrid Hypercar",
        LegoId = "42156",
        ReleaseDate = new DateOnly(2023, 3, 1),
        NumberOfParts = 1775,
        AgeFrom = 18,
        ThemeId = technicThemeId,
        Theme = null!
      },
      new LegoSet {
        Id = new Guid("00000000-0000-0000-0001-000000000008"),
        Name = "2022 Ford GT",
        LegoId = "42154",
        ReleaseDate = new DateOnly(2023, 6, 1),
        NumberOfParts = 1466,
        AgeFrom = 18,
        ThemeId = technicThemeId,
        Theme = null!
      },
      new LegoSet {
        Id = new Guid("00000000-0000-0000-0001-000000000009"),
        Name = "Mercedes-AMG F1 W14 E Performance",
        LegoId = "42171",
        ReleaseDate = new DateOnly(2024, 1, 1),
        NumberOfParts = 1642,
        AgeFrom = 18,
        ThemeId = technicThemeId,
        Theme = null!
      },
      new LegoSet {
        Id = new Guid("00000000-0000-0000-0001-000000000010"),
        Name = "Bugatti Tourbillon",
        LegoId = "42173",
        ReleaseDate = new DateOnly(2024, 6, 1),
        NumberOfParts = 3599,
        AgeFrom = 18,
        ThemeId = technicThemeId,
        Theme = null!
      },
    ]);
  }
}
