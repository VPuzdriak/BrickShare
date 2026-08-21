using BrickShare.Catalog.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrickShare.Catalog.Api.Persistence;

public sealed class CopyConfiguration : IEntityTypeConfiguration<Copy>
{
    public void Configure(EntityTypeBuilder<Copy> builder)
    {
        builder.ToTable("copies");

        builder.HasKey(copy => copy.Id);
        builder.Property(copy => copy.Id).HasColumnName("id");

        builder.Property(copy => copy.Label)
            .HasColumnName("label_code")
            .HasConversion(label => label.Value, value => LabelCode.Parse(value))
            .HasMaxLength(10)
            .IsRequired();

        builder.HasIndex(copy => copy.Label)
            .IsUnique()
            .HasDatabaseName("ix_copies_label_code");

        builder.Property(copy => copy.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(copy => copy.Grade)
            .HasColumnName("grade")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(copy => copy.RetiredAt)
            .HasColumnName("retired_at")
            .HasColumnType("timestamp with time zone");

        // Postgres maintains a system column, xmin, holding the id of the transaction that last
        // wrote the row. A uint shadow property mapped to xid, generated on every add and update
        // and marked a concurrency token, is the shape the Npgsql provider recognises: it points
        // the property at xmin, and the migration generator never emits a column for it.
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();
    }
}
