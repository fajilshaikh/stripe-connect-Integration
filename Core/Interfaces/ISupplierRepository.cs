using Core.Entities;

namespace Core.Interfaces;

public interface ISupplierRepository : IGenericRepository<Supplier>
{
    Task<Supplier?> GetByStripeAccountIdAsync(string accountId, CancellationToken ct = default);
}
