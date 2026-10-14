using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Config;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Model).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(b => b.SupplierName).HasMaxLength(200);
        builder.Property(b => b.StripeAccountId).HasMaxLength(64);
        builder.Property(b => b.PayerEmail).HasMaxLength(256);
        builder.Property(b => b.TravellerName).HasMaxLength(200);
        builder.Property(b => b.Currency).HasMaxLength(3);
        builder.Property(b => b.PaymentIntentId).HasMaxLength(64);
        builder.Property(b => b.LastError).HasMaxLength(500);
        // Timeline is a primitive collection, stored as a JSON array; the full detail is in logs/payment-flow.txt.

        builder.HasIndex(b => b.PaymentIntentId).IsUnique();
        builder.HasIndex(b => b.CreatedAt);
        builder.HasOne<Supplier>().WithMany().HasForeignKey(b => b.SupplierId).OnDelete(DeleteBehavior.Restrict);

        builder.Property<byte[]>("RowVersion").IsRowVersion();
    }
}
