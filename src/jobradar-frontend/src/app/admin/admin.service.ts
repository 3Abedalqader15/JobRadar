import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface CreateJobPostingDto {
  sourceId: string;
  title: string;
  companyName: string;
  description: string;
  location: string | null;
  isRemote: boolean;
  externalApplyUrl: string | null;
  salaryMin: number | null;
  salaryMax: number | null;
  salaryCurrency: string | null;
  employmentType: number; // 0=FullTime,1=PartTime,2=Contract,3=Freelance,4=Internship
  experienceLevel: number; // 0=Entry,1=MidLevel,2=Senior,3=Lead,4=Executive
  postedAt: string | null;
}

export interface JobPostingItem {
  id: string;
  title: string;
  companyName: string;
  location: string | null;
  isRemote: boolean;
  employmentType: number;
  experienceLevel: number;
  isActive: boolean;
  postedAt: string;
}

export interface JobPostingsResponse {
  items: JobPostingItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ApplicationItem {
  id: string;
  jobId: string;
  jobTitle: string;
  companyName: string;
  userId: string;
  userEmail: string;
  userFullName: string;
  status: string;
  appliedAt: string;
}

export interface ApplicationsResponse {
  items: ApplicationItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  constructor(private http: HttpClient) {}

  getJobPostings(page = 1, pageSize = 20, search?: string): Observable<JobPostingsResponse> {
    let params: any = { page, pageSize };
    if (search) params.search = search;
    return this.http.get<JobPostingsResponse>('/api/job-postings', { params });
  }

  createJobPosting(data: CreateJobPostingDto): Observable<string> {
    return this.http.post<string>('/api/job-postings', data);
  }

  deleteJobPosting(id: string): Observable<void> {
    return this.http.delete<void>(`/api/job-postings/${id}`);
  }

  getApplications(page = 1, pageSize = 20, jobId?: string): Observable<ApplicationsResponse> {
    let params: any = { page, pageSize };
    if (jobId) params.jobId = jobId;
    return this.http.get<ApplicationsResponse>('/api/applications', { params });
  }

  getSources(): Observable<any[]> {
    return this.http.get<any[]>('/api/sources');
  }
}
