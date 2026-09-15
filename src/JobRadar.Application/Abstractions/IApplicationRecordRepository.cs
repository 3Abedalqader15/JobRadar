using JobRadar.Domain.Entities;

namespace JobRadar.Application.Abstractions;

/// <summary>
/// Repository for <see cref="UserJobApplication"/> entities.
/// </summary>
public interface IUserJobApplicationRepository
{
    Task<UserJobApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserJobApplication>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserJobApplication>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(UserJobApplication application, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserJobApplication application, CancellationToken cancellationToken = default);
    Task DeleteAsync(UserJobApplication application, CancellationToken cancellationToken = default);
}
