namespace Core.Interfaces;

/// Webhook event-id dedupe (StripeEvents table).
public interface IStripeEventStore
{
    /// False if the event was already processed.
    Task<bool> TryMarkProcessedAsync(string eventId, string type, CancellationToken ct = default);
    Task UnmarkProcessedAsync(string eventId, CancellationToken ct = default);
}
