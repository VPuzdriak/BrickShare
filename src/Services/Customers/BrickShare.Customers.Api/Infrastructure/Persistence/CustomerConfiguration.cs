using BrickShare.Customers.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Customers.Api.Infrastructure.Persistence;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(customer => customer.Email)
            .HasColumnName("email");

        builder.Property(customer => customer.DisplayName)
            .HasColumnName("display_name");

        builder.Property(customer => customer.Phone)
            .HasColumnName("phone");

        builder.Property(customer => customer.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(customer => customer.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasDefaultValueSql("'Active'");

        builder.OwnsMany(customer => customer.Bans, ban =>
        {
            ban.ToTable("customer_bans");
            ban.WithOwner().HasForeignKey("customer_id");
            ban.HasKey(item => item.Id);

            ban.Property(item => item.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");

            ban.Property(item => item.Reason)
                .HasConversion<string>()
                .HasColumnName("reason");

            ban.Property(item => item.Comment)
                .HasColumnName("comment");

            ban.Property(item => item.BannedBy)
                .HasColumnName("banned_by");

            ban.Property(item => item.From)
                .HasColumnName("banned_from");

            ban.Property(item => item.Until)
                .HasColumnName("banned_until");

            ban.Property(item => item.LiftedAt)
                .HasColumnName("lifted_at");

            ban.Ignore(item => item.IsActive);
        });

        builder.Ignore(customer => customer.DomainEvents);
    }
}