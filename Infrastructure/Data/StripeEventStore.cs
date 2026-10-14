using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class StripeEventStore(PaymentsContext context) : IStripeEventStore
{
    public async Task<bool> TryMarkProcessedAsync(string eventId, string type, CancellationToken ct = default)
    {
        if (await context.StripeEvents.AnyAsync(e => e.Id == eventId, ct)) return false;

        context.StripeEvents.Add(new StripeEvent { Id = eventId, Type = type });
        try
        {
            await context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            return false; // another instance recorded it first (primary key violation)
        }
    }

    public Task UnmarkProcessedAsync(string eventId, CancellationToken ct = default) =>
        context.StripeEvents.Where(e => e.Id == eventId).ExecuteDeleteAsync(ct);
}
