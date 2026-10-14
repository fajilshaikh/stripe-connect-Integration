using Core.Entities;

namespace Core.Interfaces;

public interface IBookingRepository : IGenericRepository<Booking>
{
    Task<Booking?> GetByPaymentIntentIdAsync(string paymentIntentId, CancellationToken ct = default);
}
