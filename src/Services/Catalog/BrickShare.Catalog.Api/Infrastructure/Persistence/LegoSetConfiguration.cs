using BrickShare.Catalog.Domain;
using BrickShare.SharedKernel;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Infrastructure.Persistence;

public sealed class LegoSetConfiguration : IEntityTypeConfiguration<LegoSet>
{
    public void Configure(EntityTypeBuilder<LegoSet> builder)
    {
        builder.ToTable("lego_sets");

        builder.HasKey(legoSet => legoSet.Id);

        builder.Property(legoSet => legoSet.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(legoSet => legoSet.LegoSetNumber)
            .HasConversion(number => number.Value, value => new LegoSetNumber(value))
            .HasColumnName("lego_set_number");

        builder.Property(legoSet => legoSet.MinRentalDays)
            .HasConversion(days => days.Days, value => new MinRentalDays(value))
            .HasColumnName("min_rental_days");

        builder.Property(legoSet => legoSet.Name)
            .HasColumnName("name");

        builder.Property(legoSet => legoSet.IsActive)
            .HasColumnName("is_active");

        builder.ComplexProperty(legoSet => legoSet.DailyPrice, money => ConfigureMoney(money, "daily_price"));
        builder.ComplexProperty(legoSet => legoSet.Deposit, money => ConfigureMoney(money, "deposit"));
        builder.ComplexProperty(legoSet => legoSet.ReplacementValue, money => ConfigureMoney(money, "replacement_value"));
        builder.ComplexProperty(legoSet => legoSet.LateFeePerDay, money => ConfigureMoney(money, "late_fee_per_day"));

        builder.Ignore(legoSet => legoSet.DomainEvents);
    }

    private static void ConfigureMoney(ComplexPropertyBuilder<Money> money, string columnPrefix)
    {
        money.Property(value => value.Amount)
            .HasColumnName($"{columnPrefix}_amount");

        money.Property(value => value.Currency)
            .HasColumnName($"{columnPrefix}_currency")
            .HasMaxLength(3);
    }
}