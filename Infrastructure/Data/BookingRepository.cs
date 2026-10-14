using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class BookingRepository(PaymentsContext context) : GenericRepository<Booking>(context), IBookingRepository
{
    public override async Task<IReadOnlyList<Booking>> ListAllAsync(CancellationToken ct = default) =>
        await Context.Bookings.OrderByDescending(b => b.CreatedAt).ToListAsync(ct);

    public Task<Booking?> GetByPaymentIntentIdAsync(string paymentIntentId, CancellationToken ct = default) =>
        Context.Bookings.FirstOrDefaultAsync(b => b.PaymentIntentId == paymentIntentId, ct);
}
