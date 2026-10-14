using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class SupplierRepository(PaymentsContext context) : GenericRepository<Supplier>(context), ISupplierRepository
{
    public override async Task<IReadOnlyList<Supplier>> ListAllAsync(CancellationToken ct = default) =>
        await Context.Suppliers.OrderBy(s => s.Name).ToListAsync(ct);

    public Task<Supplier?> GetByStripeAccountIdAsync(string accountId, CancellationToken ct = default) =>
        Context.Suppliers.FirstOrDefaultAsync(s => s.StripeAccountId == accountId, ct);
}
