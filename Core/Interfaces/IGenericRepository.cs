using Core.Entities;

namespace Core.Interfaces;

public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default);

    /// Inserts a new entity or saves changes to a loaded one.
    Task SaveAsync(T entity, CancellationToken ct = default);
}
