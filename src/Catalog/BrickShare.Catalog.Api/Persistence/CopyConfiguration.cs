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

        builder.Property(copy => copy.CatalogSetId)
            .HasColumnName("catalog_set_id")
            .IsRequired();

        // Deleting the set should not delete the copies.
        // Deleting the set should not happen in principle, but if it does - database should refuse it
        builder.HasOne<CatalogSet>()
            .WithMany()
            .HasForeignKey(copy => copy.CatalogSetId)
            .HasConstraintName("fk_copies_catalog_set_id")
            .OnDelete(DeleteBehavior.Restrict);

        // EF would index the foreign key by convention. Naming it keeps every index in this
        // database spelled the way the rest of the schema is spelled.
        builder.HasIndex(copy => copy.CatalogSetId)
            .HasDatabaseName("ix_copies_catalog_set_id");

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

        builder.Property(copy => copy.BaselineWeightInGrams)
            .HasColumnName("baseline_weight_grams")
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
