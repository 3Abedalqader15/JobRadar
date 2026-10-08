import test from 'node:test';
import assert from 'node:assert/strict';

// EmploymentType & ExperienceLevel enum representations matching job.model.ts
const EmploymentType = {
  FullTime: 1,
  PartTime: 2,
  Contract: 3,
  Internship: 4,
  1: 'FullTime',
  2: 'PartTime',
  3: 'Contract',
  4: 'Internship'
};

const ExperienceLevel = {
  EntryLevel: 1,
  MidLevel: 2,
  Senior: 3,
  Lead: 4,
  1: 'EntryLevel',
  2: 'MidLevel',
  3: 'Senior',
  4: 'Lead'
};

const JobSortOption = {
  Relevance: 0,
  Newest: 1,
  SalaryDescending: 2
};

const MIN_TOKEN_LENGTH = 3;
const TOKEN_SEPARATORS_REGEX = /[ \-,/.(|)]+/;

function computeDesiredGroups(criteria) {
  const groups = new Set();
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
      if (empName) groups.add(`grp:emp:${empName}`);
    }
  }

  if (criteria.experienceLevels && criteria.experienceLevels.length > 0) {
    for (const exp of criteria.experienceLevels) {
      const expName = ExperienceLevel[exp];
      if (expName) groups.add(`grp:exp:${expName}`);
    }
  }

  if (criteria.skills && criteria.skills.length > 0) {
    for (const skill of criteria.skills) {
      const trimmed = skill.trim().toLowerCase();
      if (trimmed) groups.add(`grp:skill:${trimmed}`);
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

function matchesActiveFilter(job, criteria) {
  if (!job) return false;

  if (criteria.isRemote === true && !job.isRemote) return false;

  if (criteria.location && criteria.location.trim()) {
    const reqLoc = criteria.location.trim().toLowerCase();
    const jobLoc = (job.location || '').toLowerCase();
    if (!jobLoc.includes(reqLoc)) return false;
  }

  if (criteria.employmentTypes && criteria.employmentTypes.length > 0) {
    if (!criteria.employmentTypes.includes(job.employmentType)) return false;
  }

  if (criteria.experienceLevels && criteria.experienceLevels.length > 0) {
    if (!criteria.experienceLevels.includes(job.experienceLevel)) return false;
  }

  if (criteria.skills && criteria.skills.length > 0) {
    const jobSkills = (job.skills || []).map(s => s.toLowerCase());
    const matchesSkill = criteria.skills.some(s => jobSkills.includes(s.trim().toLowerCase()));
    if (!matchesSkill) return false;
  }

  if (criteria.salaryMin !== undefined && criteria.salaryMin !== null) {
    const effectiveSalary = job.salaryMax ?? job.salaryMin;
    if (effectiveSalary === undefined || effectiveSalary === null || effectiveSalary < criteria.salaryMin) return false;
  }

  if (criteria.salaryMax !== undefined && criteria.salaryMax !== null) {
    const effectiveSalary = job.salaryMin ?? job.salaryMax;
    if (effectiveSalary === undefined || effectiveSalary === null || effectiveSalary > criteria.salaryMax) return false;
  }

  if (criteria.query && criteria.query.trim()) {
    const queryTokens = criteria.query.trim().toLowerCase().split(TOKEN_SEPARATORS_REGEX)
      .filter(t => t.length >= MIN_TOKEN_LENGTH);
    if (queryTokens.length > 0) {
      const target = `${job.title} ${job.companyName} ${job.location || ''} ${(job.skills || []).join(' ')}`.toLowerCase();
      const hasMatch = queryTokens.some(token => target.includes(token));
      if (!hasMatch) return false;
    }
  }

  return true;
}

function findSortedIndex(jobs, newJob, sortBy) {
  if (jobs.length === 0) return 0;
  if (sortBy === JobSortOption.Newest) {
    const newTime = newJob.postedAt ? new Date(newJob.postedAt).getTime() : Date.now();
    for (let i = 0; i < jobs.length; i++) {
      const itemTime = jobs[i].postedAt ? new Date(jobs[i].postedAt).getTime() : 0;
      if (newTime >= itemTime) return i;
    }
    return jobs.length;
  }
  if (sortBy === JobSortOption.SalaryDescending) {
    const newSalary = newJob.salaryMax ?? newJob.salaryMin ?? -1;
    for (let i = 0; i < jobs.length; i++) {
      const itemSalary = jobs[i].salaryMax ?? jobs[i].salaryMin ?? -1;
      if (newSalary >= itemSalary) return i;
    }
    return jobs.length;
  }
  return 0;
}

function insertJobAtSortedPosition(currentJobs, newJob, sortBy, pageSize) {
  const jobsCopy = [...currentJobs];
  const existingIndex = jobsCopy.findIndex(j => j.id === newJob.id);
  if (existingIndex >= 0) {
    jobsCopy[existingIndex] = { ...jobsCopy[existingIndex], ...newJob };
    return { jobs: jobsCopy, inserted: false, isUpdate: true, targetIndex: existingIndex };
  }

  const targetIndex = findSortedIndex(jobsCopy, newJob, sortBy);
  if (targetIndex < pageSize) {
    jobsCopy.splice(targetIndex, 0, newJob);
    if (jobsCopy.length > pageSize) {
      jobsCopy.pop();
    }
    return { jobs: jobsCopy, inserted: true, isUpdate: false, targetIndex };
  }

  return { jobs: jobsCopy, inserted: false, isUpdate: false, targetIndex };
}

// ─────────────────────────────────────────────────────────────────────────────
// TESTS
// ─────────────────────────────────────────────────────────────────────────────

const sampleJob = {
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
  postedAt: '2026-10-08T10:00:00Z',
  skills: ['C#', 'Angular', 'PostgreSQL']
};

test('computeDesiredGroups returns only grp:all when no filters active', () => {
  const groups = computeDesiredGroups({});
  assert.deepEqual(Array.from(groups), ['grp:all']);
});

test('computeDesiredGroups matches backend contract for canonical criteria', () => {
  const criteria = {
    query: 'Senior .NET',
    location: 'Dubai',
    isRemote: true,
    employmentTypes: [EmploymentType.FullTime, EmploymentType.Contract],
    experienceLevels: [ExperienceLevel.Senior],
    skills: ['C#', 'Azure']
  };

  const groups = computeDesiredGroups(criteria);
  assert.equal(groups.has('grp:all'), false);
  assert.equal(groups.has('grp:remote'), true);
  assert.equal(groups.has('grp:loc:dubai'), true);
  assert.equal(groups.has('grp:emp:FullTime'), true);
  assert.equal(groups.has('grp:emp:Contract'), true);
  assert.equal(groups.has('grp:exp:Senior'), true);
  assert.equal(groups.has('grp:skill:c#'), true);
  assert.equal(groups.has('grp:skill:azure'), true);
  assert.equal(groups.has('grp:skill:senior'), true);
  assert.equal(groups.has('grp:skill:net'), true);
  assert.equal(groups.has('grp:skill:it'), false);
});

test('matchesActiveFilter enforces AND semantics across all filters', () => {
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

  assert.equal(matchesActiveFilter(sampleJob, criteria), true);

  // Reject when remote mismatch
  assert.equal(matchesActiveFilter({ ...sampleJob, isRemote: false }, { isRemote: true }), false);

  // Reject when location mismatch
  assert.equal(matchesActiveFilter(sampleJob, { location: 'London' }), false);

  // Reject when employment type mismatch
  assert.equal(matchesActiveFilter(sampleJob, { employmentTypes: [EmploymentType.PartTime] }), false);

  // Reject when experience level mismatch
  assert.equal(matchesActiveFilter(sampleJob, { experienceLevels: [ExperienceLevel.EntryLevel] }), false);

  // Reject when salaryMin exceeds job
  assert.equal(matchesActiveFilter(sampleJob, { salaryMin: 150000 }), false);

  // Handle null salary correctly
  const noSalaryJob = { ...sampleJob, salaryMin: null, salaryMax: null };
  assert.equal(matchesActiveFilter(noSalaryJob, { salaryMin: 50000 }), false);
  assert.equal(matchesActiveFilter(noSalaryJob, {}), true);
});

test('insertJobAtSortedPosition handles Newest, SalaryDescending, duplicates, and pageSize boundary', () => {
  const jobA = { ...sampleJob, id: 'a', postedAt: '2026-10-08T12:00:00Z', salaryMin: 100000, salaryMax: 100000 };
  const jobB = { ...sampleJob, id: 'b', postedAt: '2026-10-08T10:00:00Z', salaryMin: 80000, salaryMax: 80000 };
  const jobC = { ...sampleJob, id: 'c', postedAt: '2026-10-08T08:00:00Z', salaryMin: 60000, salaryMax: 60000 };

  // Newest sort: insert jobB between jobA and jobC
  const r1 = insertJobAtSortedPosition([jobA, jobC], jobB, JobSortOption.Newest, 10);
  assert.equal(r1.inserted, true);
  assert.equal(r1.targetIndex, 1);
  assert.deepEqual(r1.jobs.map(j => j.id), ['a', 'b', 'c']);

  // SalaryDescending sort: insert jobB between jobA and jobC
  const r2 = insertJobAtSortedPosition([jobA, jobC], jobB, JobSortOption.SalaryDescending, 10);
  assert.equal(r2.inserted, true);
  assert.equal(r2.targetIndex, 1);
  assert.deepEqual(r2.jobs.map(j => j.id), ['a', 'b', 'c']);

  // Duplicate: update in place
  const updatedA = { ...jobA, title: 'Updated Job A' };
  const r3 = insertJobAtSortedPosition([jobA, jobB], updatedA, JobSortOption.Newest, 10);
  assert.equal(r3.inserted, false);
  assert.equal(r3.isUpdate, true);
  assert.equal(r3.jobs[0].title, 'Updated Job A');

  // PageSize boundary: trims excess item
  const r4 = insertJobAtSortedPosition([jobA, jobC], jobB, JobSortOption.Newest, 2);
  assert.equal(r4.inserted, true);
  assert.equal(r4.jobs.length, 2);
  assert.deepEqual(r4.jobs.map(j => j.id), ['a', 'b']);

  // Beyond boundary: does not insert
  const r5 = insertJobAtSortedPosition([jobA, jobB], jobC, JobSortOption.Newest, 2);
  assert.equal(r5.inserted, false);
  assert.equal(r5.jobs.length, 2);
  assert.deepEqual(r5.jobs.map(j => j.id), ['a', 'b']);
});
