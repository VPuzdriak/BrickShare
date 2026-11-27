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

        builder.Ignore(customer => customer.DomainEvents);
        builder.Ignore(customer => customer.Status);
        builder.Ignore(customer => customer.Bans);
    }
}