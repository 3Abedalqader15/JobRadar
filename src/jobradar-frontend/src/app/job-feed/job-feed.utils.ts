import { EmploymentType, ExperienceLevel, JobSearchCriteriaDto, JobSearchResultDto, JobSortOption } from './job.model';

export const MIN_TOKEN_LENGTH = 3;
const TOKEN_SEPARATORS_REGEX = /[ \-,/.(|)]+/;

/**
 * Computes the exact SignalR relevance group keys representing the active criteria state.
 * Aligned with backend contract JobRelevanceGroups.ComputeCriteriaGroups.
 */
export function computeDesiredGroups(criteria: Partial<JobSearchCriteriaDto>): Set<string> {
  const groups = new Set<string>();

  const hasFilters = Boolean(
    (criteria.query && criteria.query.trim()) ||
    (criteria.location && criteria.location.trim()) ||
    criteria.isRemote === true ||
    (criteria.employmentTypes && criteria.employmentTypes.length > 0) ||
    (criteria.experienceLevels && criteria.experienceLevels.length > 0) ||
    (criteria.skills && criteria.skills.length > 0)
  );

  if (!hasFilters) {
    groups.add('grp:all');
    return groups;
  }

  if (criteria.isRemote === true) {
    groups.add('grp:remote');
  }

  if (criteria.location && criteria.location.trim()) {
    groups.add(`grp:loc:${criteria.location.trim().toLowerCase()}`);
  }

  if (criteria.employmentTypes && criteria.employmentTypes.length > 0) {
    for (const emp of criteria.employmentTypes) {
      const empName = EmploymentType[emp];
      if (empName) {
        groups.add(`grp:emp:${empName}`);
      }
    }
  }

  if (criteria.experienceLevels && criteria.experienceLevels.length > 0) {
    for (const exp of criteria.experienceLevels) {
      const expName = ExperienceLevel[exp];
      if (expName) {
        groups.add(`grp:exp:${expName}`);
      }
    }
  }

  if (criteria.skills && criteria.skills.length > 0) {
    for (const skill of criteria.skills) {
      const trimmed = skill.trim().toLowerCase();
      if (trimmed) {
        groups.add(`grp:skill:${trimmed}`);
      }
    }
  }

  if (criteria.query && criteria.query.trim()) {
    const tokens = criteria.query.trim().split(TOKEN_SEPARATORS_REGEX);
    for (const token of tokens) {
      const clean = token.trim().toLowerCase();
      if (clean.length >= MIN_TOKEN_LENGTH) {
        groups.add(`grp:skill:${clean}`);
      }
    }
  }

  return groups;
}

/**
 * Defense-in-depth client-side filter check.
 * Since SignalR group broadcast is OR-based while search filters are AND-based,
 * this function guarantees zero display leakage.
 */
export function matchesActiveFilter(job: JobSearchResultDto, criteria: Partial<JobSearchCriteriaDto>): boolean {
  if (!job) return false;

  // Remote check
  if (criteria.isRemote === true && !job.isRemote) {
    return false;
  }

  // Location check
  if (criteria.location && criteria.location.trim()) {
    const reqLoc = criteria.location.trim().toLowerCase();
    const jobLoc = (job.location || '').toLowerCase();
    if (!jobLoc.includes(reqLoc)) {
      return false;
    }
  }

  // Employment type check
  if (criteria.employmentTypes && criteria.employmentTypes.length > 0) {
    if (!criteria.employmentTypes.includes(job.employmentType)) {
      return false;
    }
  }

  // Experience level check
  if (criteria.experienceLevels && criteria.experienceLevels.length > 0) {
    if (!criteria.experienceLevels.includes(job.experienceLevel)) {
      return false;
    }
  }

  // Skills check (must have at least one of selected filter skills if specified)
  if (criteria.skills && criteria.skills.length > 0) {
    const jobSkills = (job.skills || []).map(s => s.toLowerCase());
    const matchesSkill = criteria.skills.some(s => jobSkills.includes(s.trim().toLowerCase()));
    if (!matchesSkill) {
      return false;
    }
  }

  // Salary range check
  if (criteria.salaryMin !== undefined && criteria.salaryMin !== null) {
    const effectiveSalary = job.salaryMax ?? job.salaryMin;
    if (effectiveSalary === undefined || effectiveSalary === null || effectiveSalary < criteria.salaryMin) {
      return false;
    }
  }

  if (criteria.salaryMax !== undefined && criteria.salaryMax !== null) {
    const effectiveSalary = job.salaryMin ?? job.salaryMax;
    if (effectiveSalary === undefined || effectiveSalary === null || effectiveSalary > criteria.salaryMax) {
      return false;
    }
  }

  // Keyword / Query check
  if (criteria.query && criteria.query.trim()) {
    const queryTokens = criteria.query.trim().toLowerCase().split(TOKEN_SEPARATORS_REGEX)
      .filter(t => t.length >= MIN_TOKEN_LENGTH);

    if (queryTokens.length > 0) {
      const searchTarget = `${job.title} ${job.companyName} ${job.location || ''} ${(job.skills || []).join(' ')}`.toLowerCase();
      const hasMatch = queryTokens.some(token => searchTarget.includes(token));
      if (!hasMatch) {
        return false;
      }
    }
  }

  return true;
}

/**
 * Computes the target index for inserting a new job based on active sort option.
 */
export function findSortedIndex(jobs: JobSearchResultDto[], newJob: JobSearchResultDto, sortBy: JobSortOption): number {
  if (jobs.length === 0) return 0;

  if (sortBy === JobSortOption.Newest) {
    const newTime = newJob.postedAt ? new Date(newJob.postedAt).getTime() : Date.now();
    for (let i = 0; i < jobs.length; i++) {
      const itemTime = jobs[i].postedAt ? new Date(jobs[i].postedAt!).getTime() : 0;
      if (newTime >= itemTime) {
        return i;
      }
    }
    return jobs.length;
  }

  if (sortBy === JobSortOption.SalaryDescending) {
    const newSalary = newJob.salaryMax ?? newJob.salaryMin ?? -1;
    for (let i = 0; i < jobs.length; i++) {
      const itemSalary = jobs[i].salaryMax ?? jobs[i].salaryMin ?? -1;
      if (newSalary >= itemSalary) {
        return i;
      }
    }
    return jobs.length;
  }

  // Default fallback: place at start
  return 0;
}

/**
 * Inserts a new job at its exact sorted position, handling in-place updates for duplicates
 * and trimming excess items to respect pageSize.
 */
export function insertJobAtSortedPosition(
  currentJobs: JobSearchResultDto[],
  newJob: JobSearchResultDto,
  sortBy: JobSortOption,
  pageSize: number
): { jobs: JobSearchResultDto[]; inserted: boolean; isUpdate: boolean; targetIndex: number } {
  const jobsCopy = [...currentJobs];

  // 1. Check for existing job (update in place)
  const existingIndex = jobsCopy.findIndex(j => j.id === newJob.id);
  if (existingIndex >= 0) {
    jobsCopy[existingIndex] = { ...jobsCopy[existingIndex], ...newJob };
    return { jobs: jobsCopy, inserted: false, isUpdate: true, targetIndex: existingIndex };
  }

  // 2. Positional live insertion
  const targetIndex = findSortedIndex(jobsCopy, newJob, sortBy);

  if (targetIndex < pageSize) {
    jobsCopy.splice(targetIndex, 0, newJob);
    if (jobsCopy.length > pageSize) {
      jobsCopy.pop(); // Trim excess item to maintain exact page size
    }
    return { jobs: jobsCopy, inserted: true, isUpdate: false, targetIndex };
  }

  // The sorted position falls beyond the current page boundary
  return { jobs: jobsCopy, inserted: false, isUpdate: false, targetIndex };
}
