import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  AdminService,
  JobPostingItem,
  ApplicationItem,
  CreateJobPostingDto,
  CompanyDto
} from './admin.service';
import { AuthService } from '../auth/auth.service';
import { ThemeService } from '../theme.service';
import { PaginationComponent } from '../shared/pagination.component';
import { Router } from '@angular/router';

type Tab = 'jobs' | 'applications' | 'create' | 'companies';

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

  // Companies state & pagination (Admin only)
  companies = signal<CompanyDto[]>([]);
  companiesTotal = signal(0);
  companiesLoading = signal(false);
  companiesPage = signal(1);
  companiesPageSize = signal(10);

  // Create Job form
  createForm: FormGroup;
  createLoading = signal(false);
  createSuccess = signal(false);
  createError = signal<string | null>(null);

  // Create Company form & modal
  createCompanyForm: FormGroup;
  showCreateCompanyModal = signal(false);
  createCompanyLoading = signal(false);
  createCompanySuccess = signal(false);
  createCompanyError = signal<string | null>(null);

  // Assign HR form & modal
  assignHrForm: FormGroup;
  showAssignHrModal = signal(false);
  selectedCompanyForHr = signal<CompanyDto | null>(null);
  assignHrLoading = signal(false);
  assignHrSuccess = signal(false);
  assignHrError = signal<string | null>(null);

  // Hard-coded source id fallback for manually posted jobs
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

    this.createCompanyForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2)]],
      slug: ['', [Validators.required, Validators.pattern(/^[a-z0-9-]+$/)]],
      logoUrl: ['']
    });

    this.assignHrForm = this.fb.group({
      userId: ['', [Validators.required]]
    });

    // Auto-slugify company name if slug is untouched
    this.createCompanyForm.get('name')?.valueChanges.subscribe(name => {
      const slugControl = this.createCompanyForm.get('slug');
      if (slugControl && (slugControl.pristine || !slugControl.value)) {
        const slug = (name || '')
          .toLowerCase()
          .trim()
          .replace(/[^a-z0-9\s-]/g, '')
          .replace(/\s+/g, '-');
        slugControl.setValue(slug, { emitEvent: false });
      }
    });
  }

  ngOnInit(): void {
    this.initCompanyScoping();
    this.loadJobs();
    this.loadApplications();
    if (this.authService.isAdmin()) {
      this.loadCompanies();
    }
  }

  private initCompanyScoping(): void {
    if (this.authService.isHR()) {
      const compName = this.authService.companyName();
      if (compName) {
        this.createForm.get('companyName')?.setValue(compName);
        this.createForm.get('companyName')?.disable();
      }
    }
  }

  setTab(tab: Tab) {
    this.activeTab.set(tab);
    if (tab === 'companies' && this.companies().length === 0) {
      this.loadCompanies();
    }
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

  loadCompanies() {
    this.companiesLoading.set(true);
    this.adminService.getCompanies(this.companiesPage(), this.companiesPageSize()).subscribe({
      next: res => {
        this.companies.set(res.items);
        this.companiesTotal.set(res.totalCount);
        this.companiesLoading.set(false);
      },
      error: () => this.companiesLoading.set(false)
    });
  }

  onCompaniesPageChange(page: number) {
    this.companiesPage.set(page);
    this.loadCompanies();
  }

  onCompaniesPageSizeChange(size: number) {
    this.companiesPageSize.set(size);
    this.companiesPage.set(1);
    this.loadCompanies();
  }

  openCreateCompanyModal() {
    this.createCompanyForm.reset();
    this.createCompanyError.set(null);
    this.createCompanySuccess.set(false);
    this.showCreateCompanyModal.set(true);
  }

  closeCreateCompanyModal() {
    this.showCreateCompanyModal.set(false);
  }

  submitCreateCompany() {
    if (this.createCompanyForm.invalid) {
      this.createCompanyForm.markAllAsTouched();
      return;
    }

    this.createCompanyLoading.set(true);
    this.createCompanyError.set(null);
    this.createCompanySuccess.set(false);

    const val = this.createCompanyForm.value;
    this.adminService.createCompany({
      name: val.name,
      slug: val.slug,
      logoUrl: val.logoUrl || null
    }).subscribe({
      next: () => {
        this.createCompanyLoading.set(false);
        this.createCompanySuccess.set(true);
        this.loadCompanies();
        setTimeout(() => {
          this.closeCreateCompanyModal();
        }, 1200);
      },
      error: err => {
        this.createCompanyLoading.set(false);
        const msg = err.error?.errors?.[0] ?? err.error?.message ?? err.error?.detail ?? 'Failed to create company.';
        this.createCompanyError.set(msg);
      }
    });
  }

  openAssignHrModal(company: CompanyDto) {
    this.selectedCompanyForHr.set(company);
    this.assignHrForm.reset();
    this.assignHrError.set(null);
    this.assignHrSuccess.set(false);
    this.showAssignHrModal.set(true);
  }

  closeAssignHrModal() {
    this.showAssignHrModal.set(false);
    this.selectedCompanyForHr.set(null);
  }

  submitAssignHr() {
    if (this.assignHrForm.invalid || !this.selectedCompanyForHr()) {
      this.assignHrForm.markAllAsTouched();
      return;
    }

    this.assignHrLoading.set(true);
    this.assignHrError.set(null);
    this.assignHrSuccess.set(false);

    const company = this.selectedCompanyForHr()!;
    this.adminService.assignHrToCompany({
      companyId: company.id,
      userId: this.assignHrForm.value.userId
    }).subscribe({
      next: () => {
        this.assignHrLoading.set(false);
        this.assignHrSuccess.set(true);
        this.loadCompanies();
        setTimeout(() => {
          this.closeAssignHrModal();
        }, 1500);
      },
      error: err => {
        this.assignHrLoading.set(false);
        const msg = err.error?.errors?.[0] ?? err.error?.message ?? err.error?.detail ?? 'Failed to assign HR user.';
        this.assignHrError.set(msg);
      }
    });
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

    const v = this.createForm.getRawValue();
    const payload: CreateJobPostingDto = {
      sourceId: this.defaultSourceId,
      title: v.title,
      companyName: v.companyName,
      companyId: this.authService.isHR() ? this.authService.companyId() : null,
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
        if (this.authService.isHR() && this.authService.companyName()) {
          this.createForm.get('companyName')?.setValue(this.authService.companyName());
          this.createForm.get('companyName')?.disable();
        }
        this.loadJobs();
        setTimeout(() => this.createSuccess.set(false), 4000);
      },
      error: err => {
        this.createLoading.set(false);
        const msg = err.error?.errors?.[0] ?? err.error?.message ?? err.error?.detail ?? 'Failed to create job posting.';
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
