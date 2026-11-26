using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Infrastructure.Persistence;

public sealed class SetImageConfiguration : IEntityTypeConfiguration<SetImage>
{
    public void Configure(EntityTypeBuilder<SetImage> builder)
    {
        builder.ToTable("set_images");

        builder.HasKey(image => image.Id);

        builder.Property(image => image.Id)
            .ValueGeneratedNever()
            .HasColumnName("id");

        builder.Property(image => image.SetId)
            .HasColumnName("set_id");

        builder.HasOne<LegoSet>()
            .WithMany()
            .HasForeignKey(image => image.SetId);

        builder.Property(image => image.OriginalBlobName)
            .HasColumnName("original_blob_name")
            .HasMaxLength(100);

        builder.Property(image => image.Status)
            .HasColumnName("status")
            .HasMaxLength(20);

        builder.Ignore(image => image.DomainEvents);
    }
}