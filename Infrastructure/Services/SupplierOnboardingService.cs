using Core.Entities;
using Core.Interfaces;
using Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Stripe;

namespace Infrastructure.Services;

/// Platform-led supplier onboarding: Jyrney creates the Express account and hands the supplier a hosted onboarding link.
/// Status is driven by `account.updated` webhooks (and a manual sync for ops).
public class SupplierOnboardingService(IStripeClient client, ISupplierRepository suppliers, IPaymentFlowLog flowLog,
    IOptions<StripeSettings> stripe, IConfiguration config) : ISupplierOnboardingService
{
    private readonly StripeSettings s = stripe.Value;

    public async Task<Supplier> CreateAsync(string name, string email, CancellationToken ct)
    {
        var supplier = new Supplier { Name = name, Email = email };
        var account = await new AccountService(client).CreateAsync(new AccountCreateOptions
        {
            Type = "express",
            Country = s.Country,
            Email = email,
            BusinessProfile = new AccountBusinessProfileOptions { Name = name, Mcc = "4121" }, // 4121 = taxicabs/limousines
            Capabilities = new AccountCapabilitiesOptions
            {
                CardPayments = new AccountCapabilitiesCardPaymentsOptions { Requested = true },
                Transfers = new AccountCapabilitiesTransfersOptions { Requested = true },
            },
            Metadata = new() { ["supplier_id"] = supplier.Id.ToString() },
        }, new RequestOptions { IdempotencyKey = $"supplier:{supplier.Id}:create" }, ct);

        supplier.StripeAccountId = account.Id;
        Apply(supplier, account);
        await suppliers.SaveAsync(supplier, ct);
        return supplier;
    }

    /// Account links are single-use and short-lived; RefreshUrl points back at the API to mint a fresh one.
    public async Task<string> CreateOnboardingLinkAsync(Supplier supplier, CancellationToken ct)
    {
        var link = await new AccountLinkService(client).CreateAsync(new AccountLinkCreateOptions
        {
            Account = supplier.StripeAccountId,
            Type = "account_onboarding",
            RefreshUrl = $"{config["App:ApiUrl"]}/api/suppliers/{supplier.Id}/onboarding-refresh",
            ReturnUrl = $"{config["App:FrontendUrl"]}/?onboarded={supplier.Id}",
        }, cancellationToken: ct);
        return link.Url;
    }

    /// Loads every connected account on the platform, so suppliers survive API restarts (the store is in-memory).
    public async Task<int> ImportFromStripeAsync(CancellationToken ct)
    {
        var count = 0;
        await foreach (var a in new AccountService(client).ListAutoPagingAsync(new AccountListOptions { Limit = 100 }, cancellationToken: ct))
        {
            var supplier = await suppliers.GetByStripeAccountIdAsync(a.Id, ct) ?? new Supplier
            {
                Id = Guid.TryParse(a.Metadata?.GetValueOrDefault("supplier_id"), out var id) ? id : Guid.NewGuid(),
                Name = a.BusinessProfile?.Name ?? a.Email ?? a.Id,
                Email = a.Email ?? "",
                StripeAccountId = a.Id,
            };
            Apply(supplier, a);
            await suppliers.SaveAsync(supplier, ct);
            count++;
        }
        flowLog.Write("Suppliers", $"Imported {count} connected account(s) from Stripe: " +
            string.Join(", ", (await suppliers.ListAllAsync(ct)).Select(x => $"{x.Name} ({x.StripeAccountId}, {x.Status})")));
        return count;
    }

    public async Task<Supplier?> SyncAsync(string accountId, CancellationToken ct)
    {
        var supplier = await suppliers.GetByStripeAccountIdAsync(accountId, ct);
        if (supplier is null) return null;
        Apply(supplier, await new AccountService(client).GetAsync(accountId, cancellationToken: ct));
        await suppliers.SaveAsync(supplier, ct);
        return supplier;
    }

    private static void Apply(Supplier supplier, Account a)
    {
        supplier.ChargesEnabled = a.ChargesEnabled;
        supplier.PayoutsEnabled = a.PayoutsEnabled;
        supplier.DisabledReason = a.Requirements?.DisabledReason;
        supplier.CurrentlyDue = a.Requirements?.CurrentlyDue?.ToList() ?? [];
        supplier.Status = (a.ChargesEnabled, a.PayoutsEnabled, a.DetailsSubmitted, supplier.DisabledReason) switch
        {
            (true, true, _, _) => OnboardingStatus.Enabled,
            (_, _, false, _) => OnboardingStatus.Invited,
            (_, _, true, null or "requirements.pending_verification") => OnboardingStatus.PendingVerification,
            _ => OnboardingStatus.Restricted,
        };
    }
}
