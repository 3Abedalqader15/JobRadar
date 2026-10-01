export enum DatePostedFilter {
  AllTime = 0,
  Last24Hours = 1,
  PastWeek = 2,
  PastMonth = 3
}

export enum JobSortOption {
  Relevance = 0,
  Newest = 1,
  SalaryDescending = 2
}

export enum EmploymentType {
  FullTime = 1,
  PartTime = 2,
  Contract = 3,
  Internship = 4
}

export enum ExperienceLevel {
  EntryLevel = 1,
  MidLevel = 2,
  Senior = 3,
  Lead = 4
}

export interface JobSearchResultDto {
  id: string;
  title: string;
  companyName: string;
  location: string;
  isRemote: boolean;
  employmentType: EmploymentType;
  experienceLevel: ExperienceLevel;
  relevanceScore: number;
  salaryMin?: number;
  salaryMax?: number;
  salaryCurrency?: string;
  postedAt?: string;
  skills?: string[];
  searchDurationMs?: number;
  isNew?: boolean;
  externalApplyUrl?: string;
}

export interface JobSearchCriteriaDto {
  query?: string;
  location?: string;
  isRemote?: boolean;
  employmentTypes?: EmploymentType[];
  experienceLevels?: ExperienceLevel[];
  salaryMin?: number;
  salaryMax?: number;
  skills?: string[];
  datePosted: DatePostedFilter;
  sortBy: JobSortOption;
  page: number;
  pageSize: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
