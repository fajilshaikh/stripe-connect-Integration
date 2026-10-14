using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Config;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.Email).HasMaxLength(256);
        builder.Property(s => s.StripeAccountId).HasMaxLength(64);
        builder.HasIndex(s => s.StripeAccountId).IsUnique();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(s => s.DisabledReason).HasMaxLength(128);
        // CurrentlyDue is a primitive collection, stored as a JSON array.

        // Webhook processing and ops actions can touch the same row; reject the stale write instead of losing one.
        builder.Property<byte[]>("RowVersion").IsRowVersion();
    }
}
