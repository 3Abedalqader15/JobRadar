namespace JobRadar.Application.Abstractions;

/// <summary>
/// Abstracts the database transaction boundary. Repositories use the same
/// DbContext instance injected per request; SaveChangesAsync persists all changes.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
