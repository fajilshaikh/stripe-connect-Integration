using System.Threading.Channels;
using Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Infrastructure.Services;

/// Stand-in for an Azure Service Bus consumer. Handlers are idempotent (event-id dedupe + re-read current state).
/// Runs as a singleton, so each event gets its own DI scope (and DbContext).
public class StripeEventProcessor(Channel<Event> queue, IServiceScopeFactory scopeFactory, ILogger<StripeEventProcessor> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var evt in queue.Reader.ReadAllAsync(ct))
        {
            using var scope = scopeFactory.CreateScope();
            var events = scope.ServiceProvider.GetRequiredService<IStripeEventStore>();
            if (!await events.TryMarkProcessedAsync(evt.Id, evt.Type, ct)) continue; // duplicate delivery
            try
            {
                switch (evt.Data.Object)
                {
                    case Account account when evt.Type == "account.updated":
                        var suppliers = scope.ServiceProvider.GetRequiredService<ISupplierOnboardingService>();
                        var supplier = await suppliers.SyncAsync(account.Id, ct);
                        log.LogInformation("Supplier {Account} is now {Status}", account.Id, supplier?.Status);
                        break;
                    case PaymentIntent pi:
                        var payments = scope.ServiceProvider.GetRequiredService<IPaymentService>();
                        var booking = await payments.SyncAsync(pi.Id, ct);
                        log.LogInformation("{Type}: booking {Booking} is {Status}", evt.Type, booking?.Id, booking?.Status);
                        break;
                    case Dispute dispute:
                        log.LogWarning("Dispute {Dispute} on {Account}: ops action required", dispute.Id, evt.Account ?? "platform");
                        break;
                    default:
                        log.LogDebug("Ignored {Type}", evt.Type);
                        break;
                }
            }
            catch (Exception e)
            {
                await events.UnmarkProcessedAsync(evt.Id, ct); // allow Stripe's retry to reprocess
                log.LogError(e, "Failed to process {EventId} ({Type})", evt.Id, evt.Type);
            }
        }
    }
}
