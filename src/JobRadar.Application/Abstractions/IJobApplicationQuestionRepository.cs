using JobRadar.Domain.Entities;

namespace JobRadar.Application.Abstractions;

public interface IJobApplicationQuestionRepository
{
    Task<IReadOnlyList<JobApplicationQuestion>> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task AddAsync(JobApplicationQuestion question, CancellationToken cancellationToken = default);
    Task UpdateAsync(JobApplicationQuestion question, CancellationToken cancellationToken = default);
    Task DeleteAsync(JobApplicationQuestion question, CancellationToken cancellationToken = default);
}
