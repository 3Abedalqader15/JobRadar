import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AdminService, JobPostingItem, ApplicationItem, CreateJobPostingDto } from './admin.service';
import { AuthService } from '../auth/auth.service';
import { ThemeService } from '../theme.service';
import { PaginationComponent } from '../shared/pagination.component';
import { Router } from '@angular/router';

type Tab = 'jobs' | 'applications' | 'create';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PaginationComponent],
  templateUrl: './admin.component.html',
  styleUrls: ['./admin.component.css']
})
export class AdminComponent implements OnInit {
  public themeService = inject(ThemeService);
  activeTab = signal<Tab>('jobs');

  // Jobs state & pagination
  jobs = signal<JobPostingItem[]>([]);
  jobsTotal = signal(0);
  jobsLoading = signal(false);
  jobsPage = signal(1);
  jobsPageSize = signal(10);

  // Applications state & pagination
  applications = signal<ApplicationItem[]>([]);
  appsTotal = signal(0);
  appsLoading = signal(false);
  appsPage = signal(1);
  appsPageSize = signal(10);

  // Create form
  createForm: FormGroup;
  createLoading = signal(false);
  createSuccess = signal(false);
  createError = signal<string | null>(null);

  // Hard-coded source id — in a real app you'd fetch from /api/sources
  defaultSourceId = '00000000-0000-0000-0000-000000000001';

  employmentTypes = [
    { value: 0, label: 'Full Time' },
    { value: 1, label: 'Part Time' },
    { value: 2, label: 'Contract' },
    { value: 3, label: 'Freelance' },
    { value: 4, label: 'Internship' },
  ];

  experienceLevels = [
    { value: 0, label: 'Entry Level' },
    { value: 1, label: 'Mid Level' },
    { value: 2, label: 'Senior' },
    { value: 3, label: 'Lead' },
    { value: 4, label: 'Executive' },
  ];

  constructor(
    private adminService: AdminService,
    public authService: AuthService,
    private fb: FormBuilder,
    private router: Router
  ) {
    this.createForm = this.fb.group({
      title: ['', [Validators.required, Validators.minLength(3)]],
      companyName: ['', [Validators.required, Validators.minLength(2)]],
      description: ['', [Validators.required, Validators.minLength(20)]],
      location: [''],
      isRemote: [false],
      externalApplyUrl: [''],
      salaryMin: [null],
      salaryMax: [null],
      salaryCurrency: ['USD'],
      employmentType: [0, Validators.required],
      experienceLevel: [1, Validators.required],
    });
  }

  ngOnInit(): void {
    this.loadJobs();
    this.loadApplications();
  }

  setTab(tab: Tab) {
    this.activeTab.set(tab);
  }

  loadJobs() {
    this.jobsLoading.set(true);
    this.adminService.getJobPostings(this.jobsPage(), this.jobsPageSize()).subscribe({
      next: res => {
        this.jobs.set(res.items);
        this.jobsTotal.set(res.totalCount);
        this.jobsLoading.set(false);
      },
      error: () => this.jobsLoading.set(false)
    });
  }

  onJobsPageChange(page: number) {
    this.jobsPage.set(page);
    this.loadJobs();
  }

  onJobsPageSizeChange(size: number) {
    this.jobsPageSize.set(size);
    this.jobsPage.set(1);
    this.loadJobs();
  }

  loadApplications() {
    this.appsLoading.set(true);
    this.adminService.getApplications(this.appsPage(), this.appsPageSize()).subscribe({
      next: res => {
        this.applications.set(res.items);
        this.appsTotal.set(res.totalCount);
        this.appsLoading.set(false);
      },
      error: () => this.appsLoading.set(false)
    });
  }

  onAppsPageChange(page: number) {
    this.appsPage.set(page);
    this.loadApplications();
  }

  onAppsPageSizeChange(size: number) {
    this.appsPageSize.set(size);
    this.appsPage.set(1);
    this.loadApplications();
  }

  deleteJob(id: string) {
    if (!confirm('Are you sure you want to delete this job posting?')) return;
    this.adminService.deleteJobPosting(id).subscribe({
      next: () => {
        this.jobs.update(jobs => jobs.filter(j => j.id !== id));
        this.jobsTotal.update(c => c - 1);
      },
      error: err => alert('Failed to delete: ' + (err.error?.message ?? err.message))
    });
  }

  submitCreate() {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }

    this.createLoading.set(true);
    this.createSuccess.set(false);
    this.createError.set(null);

    const v = this.createForm.value;
    const payload: CreateJobPostingDto = {
      sourceId: this.defaultSourceId,
      title: v.title,
      companyName: v.companyName,
      description: v.description,
      location: v.location || null,
      isRemote: v.isRemote,
      externalApplyUrl: v.externalApplyUrl || null,
      salaryMin: v.salaryMin ? Number(v.salaryMin) : null,
      salaryMax: v.salaryMax ? Number(v.salaryMax) : null,
      salaryCurrency: v.salaryCurrency || 'USD',
      employmentType: Number(v.employmentType),
      experienceLevel: Number(v.experienceLevel),
      postedAt: null,
    };

    this.adminService.createJobPosting(payload).subscribe({
      next: () => {
        this.createLoading.set(false);
        this.createSuccess.set(true);
        this.createForm.reset({
          isRemote: false,
          employmentType: 0,
          experienceLevel: 1,
          salaryCurrency: 'USD'
        });
        this.loadJobs();
        setTimeout(() => this.createSuccess.set(false), 4000);
      },
      error: err => {
        this.createLoading.set(false);
        const msg = err.error?.errors?.[0] ?? err.error?.message ?? 'Failed to create job posting.';
        this.createError.set(msg);
      }
    });
  }

  getEmploymentLabel(val: number): string {
    return this.employmentTypes.find(t => t.value === val)?.label ?? 'Unknown';
  }

  getExperienceLabel(val: number): string {
    return this.experienceLevels.find(t => t.value === val)?.label ?? 'Unknown';
  }

  logout() {
    this.authService.logout().subscribe({
      next: () => this.router.navigate(['/login']),
      error: () => {
        this.authService.logoutLocal();
      }
    });
  }
}
