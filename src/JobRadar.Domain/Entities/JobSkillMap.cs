namespace JobRadar.Domain.Entities;

/// <summary>
/// Join entity for the many-to-many relationship between Job and Skill.
/// Uses a composite primary key (JobId, SkillId).
/// </summary>
public sealed class JobSkillMap
{
    public Guid JobId { get; private set; }
    public Guid SkillId { get; private set; }

    // Navigations
    public Job? Job { get; private set; }
    public Skill? Skill { get; private set; }

    // EF Core constructor
    private JobSkillMap() { }

    public JobSkillMap(Guid jobId, Guid skillId)
    {
        JobId = jobId;
        SkillId = skillId;
    }
}
