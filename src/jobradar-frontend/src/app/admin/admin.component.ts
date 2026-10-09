import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, FormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  AdminService,
  JobPostingItem,
  ApplicationItem,
  CreateJobPostingDto,
  CompanyDto,
  JobQuestionDto,
  QuestionType
} from './admin.service';
import { AuthService } from '../auth/auth.service';
import { ThemeService } from '../theme.service';
import { PaginationComponent } from '../shared/pagination.component';
import { Router } from '@angular/router';

type Tab = 'jobs' | 'applications' | 'create' | 'companies';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterModule, PaginationComponent],
  templateUrl: './admin.component.html',
  styleUrls: ['./admin.component.css']
})
export class AdminComponent implements OnInit {
  public themeService = inject(ThemeService);
  activeTab = signal<Tab>('jobs');

  // QuestionType enum exposed to template
  QuestionType = QuestionType;

  // Jobs state & pagination
  jobs = signal<JobPostingItem[]>([]);
  jobsTotal = signal(0);
  jobsLoading = signal(false);
  jobsPage = signal(1);
  jobsPageSize = signal(10);
  jobsSourceFilter = signal<'all' | 'direct' | 'crawled'>('all');
  crawlerTriggering = signal(false);
  crawlerMessage = signal<string | null>(null);

  // Applications state & pagination & sorting
  applications = signal<ApplicationItem[]>([]);
  appsTotal = signal(0);
  appsLoading = signal(false);
  appsPage = signal(1);
  appsPageSize = signal(10);
  appsSortBy = signal<string>('date_desc');
  selectedAppForDetails = signal<ApplicationItem | null>(null);
  showAppDetailsModal = signal(false);
  downloadingCvId = signal<string | null>(null);
  retryingAnalysisId = signal<string | null>(null);

  // Screening Questions Builder state
  showQuestionsModal = signal(false);
  selectedJobForQuestions = signal<JobPostingItem | null>(null);
  questionsList = signal<JobQuestionDto[]>([]);
  questionsLoading = signal(false);
  questionsSaving = signal(false);
  questionsSuccess = signal(false);
  questionsError = signal<string | null>(null);

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

  setJobsSourceFilter(filter: 'all' | 'direct' | 'crawled'): void {
    this.jobsSourceFilter.set(filter);
    this.jobsPage.set(1);
    this.loadJobs();
  }

  loadJobs() {
    this.jobsLoading.set(true);
    const filter = this.jobsSourceFilter() === 'all' ? undefined : this.jobsSourceFilter();
    this.adminService.getJobPostings(this.jobsPage(), this.jobsPageSize(), undefined, filter).subscribe({
      next: res => {
        this.jobs.set(res.items);
        this.jobsTotal.set(res.totalCount);
        this.jobsLoading.set(false);
      },
      error: () => this.jobsLoading.set(false)
    });
  }

  triggerCrawler(): void {
    if (this.crawlerTriggering()) return;
    this.crawlerTriggering.set(true);
    this.crawlerMessage.set('Crawling live global & MENA boards (Jobicy, RemoteOK, WWR, Tanqeeb)...');

    this.adminService.triggerCrawlerIngestion().subscribe({
      next: res => {
        this.crawlerTriggering.set(false);
        this.crawlerMessage.set(res.message || `Crawler completed. ${res.newJobsCreated} new jobs ingested.`);
        this.loadJobs();
        setTimeout(() => this.crawlerMessage.set(null), 8000);
      },
      error: err => {
        this.crawlerTriggering.set(false);
        this.crawlerMessage.set(err.error?.message || 'Crawler execution encountered an error.');
        setTimeout(() => this.crawlerMessage.set(null), 8000);
      }
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
    this.adminService.getApplications(this.appsPage(), this.appsPageSize(), undefined, this.appsSortBy()).subscribe({
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

  onAppsSortChange(sortBy: string) {
    this.appsSortBy.set(sortBy);
    this.appsPage.set(1);
    this.loadApplications();
  }

  downloadCv(app: ApplicationItem, openInNewTab = false) {
    if (!app.cvFilePath && !app.cvOriginalFileName) return;
    this.downloadingCvId.set(app.id);
    this.adminService.downloadApplicationCv(app.id).subscribe({
      next: (blob) => {
        this.downloadingCvId.set(null);
        const fileName = app.cvOriginalFileName || `${app.applicantFullName || app.userFullName || 'applicant'}_cv.pdf`;
        const isDocx = fileName.toLowerCase().endsWith('.docx');
        const mimeType = isDocx
          ? 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
          : 'application/pdf';

        const fileBlob = new Blob([blob], { type: mimeType });
        const url = window.URL.createObjectURL(fileBlob);

        if (openInNewTab && !isDocx) {
          window.open(url, '_blank');
        } else {
          const a = document.createElement('a');
          a.style.display = 'none';
          a.href = url;
          a.download = fileName;
          document.body.appendChild(a);
          a.click();
          document.body.removeChild(a);
        }

        setTimeout(() => window.URL.revokeObjectURL(url), 60000);
      },
      error: (err) => {
        this.downloadingCvId.set(null);
        let msg = err.error?.message ?? err.statusText ?? 'Unauthorized or file missing';
        if (err.status === 0) {
          msg = 'تعذر تحميل الملف بسبب اعتراض إضافة التنزيل في المتصفح (IDM). يرجى الضغط باستمرار على زر Alt عند النقر على الزر لتجاوز IDM، أو إيقاف إضافة IDM لـ localhost.';
        }
        alert('Failed to download CV: ' + msg);
      }
    });
  }

  retryAnalysis(app: ApplicationItem) {
    this.retryingAnalysisId.set(app.id);
    this.adminService.retryCvAnalysis(app.id).subscribe({
      next: () => {
        this.retryingAnalysisId.set(null);
        this.loadApplications();
        if (this.selectedAppForDetails()?.id === app.id) {
          this.adminService.getApplications(this.appsPage(), this.appsPageSize(), undefined, this.appsSortBy()).subscribe(res => {
            const updated = res.items.find(i => i.id === app.id);
            if (updated) this.selectedAppForDetails.set(updated);
          });
        }
      },
      error: (err) => {
        this.retryingAnalysisId.set(null);
        const msg = err.error?.message ?? err.error?.detail ?? err.message ?? 'Failed to trigger AI CV analysis retry.';
        alert('Failed to retry AI analysis: ' + msg);
      }
    });
  }

  openAppDetails(app: ApplicationItem) {
    this.selectedAppForDetails.set(app);
    this.showAppDetailsModal.set(true);
  }

  closeAppDetails() {
    this.showAppDetailsModal.set(false);
    this.selectedAppForDetails.set(null);
  }

  getScoreBadgeClass(app: ApplicationItem): string {
    const status = (app.aiAnalysisStatus || '').toLowerCase();
    if (status === 'processing' || status === 'pending') {
      return 'badge-ai-pulsing';
    }
    if (status === 'failed') {
      return 'badge-ai-failed';
    }
    if (status === 'insufficientjobdescription') {
      return 'badge-ai-insufficient';
    }
    if (app.aiMatchScore == null) {
      return 'badge-ai-none';
    }
    if (app.aiMatchScore >= 80) {
      return 'badge-score-high';
    }
    if (app.aiMatchScore >= 50) {
      return 'badge-score-mid';
    }
    return 'badge-score-low';
  }

  getScoreText(app: ApplicationItem): string {
    const status = (app.aiAnalysisStatus || '').toLowerCase();
    if (status === 'processing') return 'Analyzing...';
    if (status === 'pending') return 'Queued';
    if (status === 'failed') return 'Failed';
    if (status === 'insufficientjobdescription') return 'Insufficient Job Description';
    if (app.aiMatchScore != null) return `${app.aiMatchScore}% Match`;
    return 'No Score';
  }

  getParsedBreakdown(breakdownJson?: string | null): any {
    if (!breakdownJson) return null;
    try {
      return JSON.parse(breakdownJson);
    } catch {
      return null;
    }
  }

  getParsedEvidence(evidenceJson?: string | null): Array<{ keyword: string; evidenceQuote: string }> {
    if (!evidenceJson) return [];
    try {
      return JSON.parse(evidenceJson);
    } catch {
      return [];
    }
  }

  // ── Screening Questions Builder Methods ─────────────────────────
  openQuestionsModal(job: JobPostingItem) {
    this.selectedJobForQuestions.set(job);
    this.questionsList.set([]);
    this.questionsLoading.set(true);
    this.questionsError.set(null);
    this.questionsSuccess.set(false);
    this.showQuestionsModal.set(true);

    this.adminService.getJobQuestions(job.id).subscribe({
      next: (questions) => {
        this.questionsList.set(questions || []);
        this.questionsLoading.set(false);
      },
      error: () => {
        this.questionsLoading.set(false);
        this.questionsError.set('Failed to load existing screening questions.');
      }
    });
  }

  closeQuestionsModal() {
    this.showQuestionsModal.set(false);
    this.selectedJobForQuestions.set(null);
    this.questionsList.set([]);
    this.questionsError.set(null);
    this.questionsSuccess.set(false);
  }

  addQuestion() {
    const current = this.questionsList();
    const newQ: JobQuestionDto = {
      questionText: '',
      questionType: QuestionType.Text,
      options: [],
      isRequired: true,
      displayOrder: current.length + 1
    };
    this.questionsList.set([...current, newQ]);
  }

  removeQuestion(index: number) {
    const updated = this.questionsList().filter((_, i) => i !== index);
    this.questionsList.set(updated.map((q, i) => ({ ...q, displayOrder: i + 1 })));
  }

  moveQuestionUp(index: number) {
    if (index <= 0) return;
    const list = [...this.questionsList()];
    const temp = list[index];
    list[index] = list[index - 1];
    list[index - 1] = temp;
    this.questionsList.set(list.map((q, i) => ({ ...q, displayOrder: i + 1 })));
  }

  moveQuestionDown(index: number) {
    const list = [...this.questionsList()];
    if (index >= list.length - 1) return;
    const temp = list[index];
    list[index] = list[index + 1];
    list[index + 1] = temp;
    this.questionsList.set(list.map((q, i) => ({ ...q, displayOrder: i + 1 })));
  }

  addOption(q: JobQuestionDto, optInput: HTMLInputElement) {
    const val = optInput.value.trim();
    if (!val) return;
    if (!q.options) q.options = [];
    q.options.push(val);
    optInput.value = '';
  }

  removeOption(q: JobQuestionDto, optIdx: number) {
    if (!q.options) return;
    q.options.splice(optIdx, 1);
  }

  saveQuestions() {
    const job = this.selectedJobForQuestions();
    if (!job) return;

    const list = this.questionsList();
    for (let i = 0; i < list.length; i++) {
      const q = list[i];
      if (!q.questionText || !q.questionText.trim()) {
        this.questionsError.set(`Question #${i + 1} cannot have empty text.`);
        return;
      }
      if (q.questionType === QuestionType.MultipleChoice && (!q.options || q.options.length < 2)) {
        this.questionsError.set(`Question #${i + 1} is Multiple Choice and must have at least 2 options.`);
        return;
      }
    }

    this.questionsSaving.set(true);
    this.questionsError.set(null);
    this.questionsSuccess.set(false);

    const payload = list.map((q, idx) => ({
      ...q,
      questionText: q.questionText.trim(),
      displayOrder: idx + 1
    }));

    this.adminService.saveJobQuestions(job.id, payload).subscribe({
      next: () => {
        this.questionsSaving.set(false);
        this.questionsSuccess.set(true);
        setTimeout(() => {
          this.questionsSuccess.set(false);
          this.closeQuestionsModal();
        }, 1200);
      },
      error: (err) => {
        this.questionsSaving.set(false);
        const msg = err.error?.message || err.error?.detail || err.error?.errors?.[0] || 'Failed to save screening questions.';
        this.questionsError.set(msg);
      }
    });
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
