using Core.Entities;

namespace Core.Interfaces;

/// Booking payment lifecycle: authorize (quote + buffer) -> capture final price | cancel (release or fee) -> refund.
public interface IPaymentService
{
    Task<(Booking Booking, string ClientSecret)> CreateBookingAsync(CommercialModel model, Guid? supplierId,
        string payerEmail, string travellerName, long quoteAmount, int? commissionBps, CancellationToken ct);
    Task<Booking> CompleteAsync(Guid id, long finalAmount, CancellationToken ct);
    Task<Booking> CancelAsync(Guid id, long cancellationFee, CancellationToken ct);
    Task<Booking> RefundAsync(Guid id, long amount, CancellationToken ct);
    Task<Booking?> SyncAsync(string paymentIntentId, CancellationToken ct);
}
