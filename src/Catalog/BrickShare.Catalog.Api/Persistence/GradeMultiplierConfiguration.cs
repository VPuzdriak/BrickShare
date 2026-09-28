using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Persistence;

public sealed record GradeMultiplier(ConditionGrade Grade, decimal Multiplier);

public sealed class GradeMultiplierConfiguration : IEntityTypeConfiguration<GradeMultiplier>
{
    public void Configure(EntityTypeBuilder<GradeMultiplier> builder)
    {
        builder.ToTable("grade_multipliers", table =>
            table.HasCheckConstraint("ck_grade_multipliers_positive", "multiplier > 0"));

        builder.HasKey(row => row.Grade);

        builder.Property(row => row.Grade)
            .HasColumnName("grade")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(row => row.Multiplier)
            .HasColumnName("multiplier")
            .HasPrecision(4, 2)
            .IsRequired();

        // Placeholders until an admin edits them but a shop with no prices cannot open.
        builder.HasData(
            new GradeMultiplier(ConditionGrade.New, 1.00m),
            new GradeMultiplier(ConditionGrade.Excellent, 0.85m),
            new GradeMultiplier(ConditionGrade.Good, 0.70m),
            new GradeMultiplier(ConditionGrade.Fair, 0.55m));
    }
}
