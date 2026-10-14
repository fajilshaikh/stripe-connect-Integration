using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class GenericRepository<T>(PaymentsContext context) : IGenericRepository<T> where T : BaseEntity
{
    protected readonly PaymentsContext Context = context;

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await Context.Set<T>().FindAsync([id], ct);

    public virtual async Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default) =>
        await Context.Set<T>().ToListAsync(ct);

    public async Task SaveAsync(T entity, CancellationToken ct = default)
    {
        if (Context.Entry(entity).State == EntityState.Detached) Context.Set<T>().Add(entity);
        await Context.SaveChangesAsync(ct);
    }
}
