import { computeDesiredGroups, matchesActiveFilter, findSortedIndex, insertJobAtSortedPosition } from './job-feed.utils';
import { EmploymentType, ExperienceLevel, JobSearchResultDto, JobSortOption, DatePostedFilter } from './job.model';

describe('JobFeedUtils', () => {
  const sampleJob: JobSearchResultDto = {
    id: 'job-1',
    title: 'Senior .NET Full Stack Engineer',
    companyName: 'TechCorp',
    location: 'Amman, Jordan',
    isRemote: true,
    employmentType: EmploymentType.FullTime,
    experienceLevel: ExperienceLevel.Senior,
    relevanceScore: 0,
    salaryMin: 90000,
    salaryMax: 120000,
    salaryCurrency: 'USD',
    postedAt: '2026-10-08T10:00:00Z',
    skills: ['C#', 'Angular', 'PostgreSQL']
  };

  describe('computeDesiredGroups', () => {
    it('should return only grp:all when no criteria are active', () => {
      const groups = computeDesiredGroups({});
      expect(Array.from(groups)).toEqual(['grp:all']);
    });

    it('should match backend contract for canonical criteria', () => {
      const criteria = {
        query: 'Senior .NET',
        location: 'Dubai',
        isRemote: true,
        employmentTypes: [EmploymentType.FullTime, EmploymentType.Contract],
        experienceLevels: [ExperienceLevel.Senior],
        skills: ['C#', 'Azure'],
        datePosted: DatePostedFilter.AllTime,
        sortBy: JobSortOption.Newest,
        page: 1,
        pageSize: 20
      };

      const groups = computeDesiredGroups(criteria);

      expect(groups.has('grp:all')).toBeFalse();
      expect(groups.has('grp:remote')).toBeTrue();
      expect(groups.has('grp:loc:dubai')).toBeTrue();
      expect(groups.has('grp:emp:FullTime')).toBeTrue();
      expect(groups.has('grp:emp:Contract')).toBeTrue();
      expect(groups.has('grp:exp:Senior')).toBeTrue();
      expect(groups.has('grp:skill:c#')).toBeTrue();
      expect(groups.has('grp:skill:azure')).toBeTrue();
      expect(groups.has('grp:skill:senior')).toBeTrue();
      expect(groups.has('grp:skill:net')).toBeTrue();
      // Short token (length < 3) must NOT be present
      expect(groups.has('grp:skill:it')).toBeFalse();
    });
  });

  describe('matchesActiveFilter (AND Semantics Guard)', () => {
    it('should return true when all active criteria match', () => {
      const criteria = {
        isRemote: true,
        location: 'Amman',
        employmentTypes: [EmploymentType.FullTime],
        experienceLevels: [ExperienceLevel.Senior],
        skills: ['C#'],
        salaryMin: 80000,
        salaryMax: 130000,
        query: 'Engineer'
      };

      expect(matchesActiveFilter(sampleJob, criteria)).toBeTrue();
    });

    it('should reject job if remote filter does not match', () => {
      const nonRemoteJob = { ...sampleJob, isRemote: false };
      expect(matchesActiveFilter(nonRemoteJob, { isRemote: true })).toBeFalse();
    });

    it('should reject job if location filter does not match', () => {
      expect(matchesActiveFilter(sampleJob, { location: 'London' })).toBeFalse();
    });

    it('should reject job if employmentType does not match', () => {
      expect(matchesActiveFilter(sampleJob, { employmentTypes: [EmploymentType.PartTime] })).toBeFalse();
    });

    it('should reject job if experienceLevel does not match', () => {
      expect(matchesActiveFilter(sampleJob, { experienceLevels: [ExperienceLevel.EntryLevel] })).toBeFalse();
    });

    it('should reject job if required skills are missing', () => {
      expect(matchesActiveFilter(sampleJob, { skills: ['Rust', 'Go'] })).toBeFalse();
    });

    it('should reject job if salaryMin is above job salary', () => {
      expect(matchesActiveFilter(sampleJob, { salaryMin: 150000 })).toBeFalse();
    });

    it('should handle null salary correctly', () => {
      const noSalaryJob = { ...sampleJob, salaryMin: undefined, salaryMax: undefined };
      // When user filters by salaryMin, job with no salary should not match
      expect(matchesActiveFilter(noSalaryJob, { salaryMin: 50000 })).toBeFalse();
      // When no salary filter active, job with no salary should match
      expect(matchesActiveFilter(noSalaryJob, {})).toBeTrue();
    });
  });

  describe('insertJobAtSortedPosition', () => {
    const jobA: JobSearchResultDto = { ...sampleJob, id: 'a', postedAt: '2026-10-08T12:00:00Z', salaryMin: 100000, salaryMax: 100000 };
    const jobB: JobSearchResultDto = { ...sampleJob, id: 'b', postedAt: '2026-10-08T10:00:00Z', salaryMin: 80000, salaryMax: 80000 };
    const jobC: JobSearchResultDto = { ...sampleJob, id: 'c', postedAt: '2026-10-08T08:00:00Z', salaryMin: 60000, salaryMax: 60000 };

    it('should insert in correct position for JobSortOption.Newest', () => {
      const current = [jobA, jobC];
      // jobB was posted at 10:00, which is between jobA (12:00) and jobC (08:00)
      const result = insertJobAtSortedPosition(current, jobB, JobSortOption.Newest, 10);
      expect(result.inserted).toBeTrue();
      expect(result.targetIndex).toBe(1);
      expect(result.jobs.map(j => j.id)).toEqual(['a', 'b', 'c']);
    });

    it('should insert in correct position for JobSortOption.SalaryDescending', () => {
      const current = [jobA, jobC];
      // jobB has salary 80000, between jobA (100000) and jobC (60000)
      const result = insertJobAtSortedPosition(current, jobB, JobSortOption.SalaryDescending, 10);
      expect(result.inserted).toBeTrue();
      expect(result.targetIndex).toBe(1);
      expect(result.jobs.map(j => j.id)).toEqual(['a', 'b', 'c']);
    });

    it('should update existing job in place (duplicate prevention)', () => {
      const current = [jobA, jobB];
      const updatedA: JobSearchResultDto = { ...jobA, title: 'Updated Job A' };

      const result = insertJobAtSortedPosition(current, updatedA, JobSortOption.Newest, 10);
      expect(result.inserted).toBeFalse();
      expect(result.isUpdate).toBeTrue();
      expect(result.jobs.length).toBe(2);
      expect(result.jobs[0].title).toBe('Updated Job A');
    });

    it('should enforce pageSize boundary by trimming excess items', () => {
      const current = [jobA, jobC]; // length 2, pageSize = 2
      // Inserting jobB should place it at index 1 and trim jobC so length remains 2
      const result = insertJobAtSortedPosition(current, jobB, JobSortOption.Newest, 2);
      expect(result.inserted).toBeTrue();
      expect(result.jobs.length).toBe(2);
      expect(result.jobs.map(j => j.id)).toEqual(['a', 'b']);
    });

    it('should not insert when target index falls beyond pageSize', () => {
      const current = [jobA, jobB]; // length 2, pageSize = 2
      // Inserting jobC (older) has targetIndex 2 >= pageSize 2
      const result = insertJobAtSortedPosition(current, jobC, JobSortOption.Newest, 2);
      expect(result.inserted).toBeFalse();
      expect(result.jobs.length).toBe(2);
      expect(result.jobs.map(j => j.id)).toEqual(['a', 'b']);
    });
  });
});
