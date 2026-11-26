using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Infrastructure.Persistence;

public sealed class SetInstanceConfiguration : IEntityTypeConfiguration<SetInstance>
{
    public void Configure(EntityTypeBuilder<SetInstance> builder)
    {
        builder.ToTable("set_instances");

        builder.HasKey(instance => instance.Id);

        builder.Property(instance => instance.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(instance => instance.SetId)
            .HasColumnName("set_id");

        builder.HasOne<LegoSet>()
            .WithMany()
            .HasForeignKey(instance => instance.SetId);

        builder.Property(instance => instance.InventoryCode)
            .HasConversion(code => code.Value, value => InventoryCode.Parse(value).Value)
            .HasColumnName("inventory_code")
            .HasMaxLength(7);

        builder.HasIndex(instance => instance.InventoryCode)
            .IsUnique();

        builder.Property(instance => instance.Condition)
            .HasColumnName("condition")
            .HasMaxLength(10);

        builder.Property(instance => instance.Status)
            .HasColumnName("status")
            .HasMaxLength(20);

        builder.Ignore(instance => instance.DomainEvents);
    }
}