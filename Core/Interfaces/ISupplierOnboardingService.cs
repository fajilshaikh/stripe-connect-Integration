using Core.Entities;

namespace Core.Interfaces;

/// Platform-led supplier onboarding (Stripe Connect Express accounts).
public interface ISupplierOnboardingService
{
    Task<Supplier> CreateAsync(string name, string email, CancellationToken ct);
    Task<string> CreateOnboardingLinkAsync(Supplier supplier, CancellationToken ct);
    Task<int> ImportFromStripeAsync(CancellationToken ct);
    Task<Supplier?> SyncAsync(string accountId, CancellationToken ct);
}
