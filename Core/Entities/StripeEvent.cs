namespace Core.Entities;

/// A processed webhook event, kept so duplicate deliveries are ignored (Stripe retries for up to 3 days).
public class StripeEvent
{
    public required string Id { get; init; }  // evt_...
    public required string Type { get; init; }
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
}
