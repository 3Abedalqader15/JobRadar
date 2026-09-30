export interface JobSearchResultDto {
  id: string;
  title: string;
  companyName: string;
  location: string;
  isRemote: boolean;
  employmentType: number;
  experienceLevel: number;
  relevanceScore: number;
  postedAt?: string; // Optional field for UI
  skills?: string[]; // Optional field for UI chips
  isNew?: boolean;   // UI state to mark newly pushed jobs
  externalApplyUrl?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
