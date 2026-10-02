import { Component, OnInit, OnDestroy, effect, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { Subscription } from 'rxjs';
import { JobFeedService } from './job-feed.service';
import {
  JobSearchResultDto,
  DatePostedFilter,
  JobSortOption,
  EmploymentType,
  ExperienceLevel,
  JobSearchCriteriaDto
} from './job.model';
import { AuthService } from '../auth/auth.service';
import { NotificationService } from '../notifications/notification.service';

@Component({
  selector: 'app-job-feed',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './job-feed.component.html',
  styleUrls: ['./job-feed.component.css']
})
export class JobFeedComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  public jobService = inject(JobFeedService);
  public authService = inject(AuthService);
  private notificationService = inject(NotificationService);
  private router = inject(Router);

  filterForm: FormGroup;

  jobs = signal<JobSearchResultDto[]>([]);
  totalCount = signal<number>(0);
  loading = signal<boolean>(false);
  syncing = signal<boolean>(false);
  searchLatencyMs = signal<number | null>(null);

  // Popular skills cloud for 1-click filtering
  popularSkills: string[] = ['.NET', 'C#', 'Angular', 'TypeScript', 'PostgreSQL', 'Docker', 'Python', 'React', 'AWS', 'Kubernetes'];
  selectedSkills = signal<string[]>([]);

  private formSub!: Subscription;

  employmentTypes = [
    { value: EmploymentType.FullTime, label: 'Full Time' },
    { value: EmploymentType.PartTime, label: 'Part Time' },
    { value: EmploymentType.Contract, label: 'Contract' },
    { value: EmploymentType.Internship, label: 'Internship' }
  ];

  experienceLevels = [
    { value: ExperienceLevel.EntryLevel, label: 'Entry Level' },
    { value: ExperienceLevel.MidLevel, label: 'Mid Level' },
    { value: ExperienceLevel.Senior, label: 'Senior' },
    { value: ExperienceLevel.Lead, label: 'Lead' }
  ];

  datePostedOptions = [
    { value: DatePostedFilter.AllTime, label: 'All Time' },
    { value: DatePostedFilter.Last24Hours, label: 'Past 24 Hours' },
    { value: DatePostedFilter.PastWeek, label: 'Past Week' },
    { value: DatePostedFilter.PastMonth, label: 'Past Month' }
  ];

  sortOptions = [
    { value: JobSortOption.Relevance, label: 'AI Best Match' },
    { value: JobSortOption.Newest, label: 'Newest First' },
    { value: JobSortOption.SalaryDescending, label: 'Highest Salary' }
  ];

  constructor() {
    this.filterForm = this.fb.group({
      query: [''],
      location: [''],
      isRemote: [false],
      employmentType: [null],
      experienceLevel: [null],
      salaryMin: [null],
      salaryMax: [null],
      datePosted: [DatePostedFilter.AllTime],
      sortBy: [JobSortOption.Relevance]
    });

    // Reactive effect for new matching jobs received via SignalR
    effect(() => {
      const newJob = this.jobService.newJobSignal();
      if (newJob) {
        this.jobs.update(currentJobs => [newJob, ...currentJobs]);
        this.totalCount.update(count => count + 1);
      }
    }, { allowSignalWrites: true });
  }

  ngOnInit(): void {
    this.jobService.startSignalRConnection();
    this.loadJobs();

    this.formSub = this.filterForm.valueChanges
      .pipe(
        debounceTime(350),
        distinctUntilChanged((prev, curr) => JSON.stringify(prev) === JSON.stringify(curr))
      )
      .subscribe(() => this.loadJobs());
  }

  ngOnDestroy(): void {
    this.formSub?.unsubscribe();
    this.jobService.stopSignalRConnection();
  }

  toggleSkill(skill: string): void {
    const current = this.selectedSkills();
    if (current.includes(skill)) {
      this.selectedSkills.set(current.filter(s => s !== skill));
    } else {
      if (current.length >= 10) {
        this.notificationService.show({
          type: 'warning',
          title: 'Skill limit reached',
          message: 'Maximum 10 skill filters allowed at once.'
        });
        return;
      }
      this.selectedSkills.set([...current, skill]);
    }
    this.loadJobs();
  }

  toggleRemotePreset(): void {
    const currentVal = this.filterForm.get('isRemote')?.value;
    this.filterForm.patchValue({ isRemote: !currentVal });
  }

  setTodayPreset(): void {
    this.filterForm.patchValue({ datePosted: DatePostedFilter.Last24Hours });
  }

  setHighSalaryPreset(): void {
    this.filterForm.patchValue({ salaryMin: 100000 });
  }

  resetFilters(): void {
    this.selectedSkills.set([]);
    this.filterForm.reset({
      query: '',
      location: '',
      isRemote: false,
      employmentType: null,
      experienceLevel: null,
      salaryMin: null,
      salaryMax: null,
      datePosted: DatePostedFilter.AllTime,
      sortBy: JobSortOption.Relevance
    });
  }

  loadJobs(): void {
    this.loading.set(true);
    const formVals = this.filterForm.value;

    const criteria: Partial<JobSearchCriteriaDto> = {
      query: formVals.query?.trim() || undefined,
      location: formVals.location?.trim() || undefined,
      isRemote: formVals.isRemote || undefined,
      employmentTypes: formVals.employmentType != null && formVals.employmentType !== 'null'
        ? [Number(formVals.employmentType)]
        : undefined,
      experienceLevels: formVals.experienceLevel != null && formVals.experienceLevel !== 'null'
        ? [Number(formVals.experienceLevel)]
        : undefined,
      salaryMin: formVals.salaryMin ? Number(formVals.salaryMin) : undefined,
      salaryMax: formVals.salaryMax ? Number(formVals.salaryMax) : undefined,
      skills: this.selectedSkills().length > 0 ? this.selectedSkills() : undefined,
      datePosted: Number(formVals.datePosted) || DatePostedFilter.AllTime,
      sortBy: Number(formVals.sortBy) || JobSortOption.Relevance,
      page: 1,
      pageSize: 24
    };

    // Keep SignalR relevance groups synchronized with current criteria
    this.jobService.updateCriteriaSubscriptions(criteria);

    const startTime = performance.now();

    this.jobService.searchJobs(criteria).subscribe({
      next: (res) => {
        this.jobs.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
        const duration = Math.round(performance.now() - startTime);
        this.searchLatencyMs.set(res.items.length > 0 && res.items[0].searchDurationMs != null 
          ? res.items[0].searchDurationMs 
          : duration);
      },
      error: (err) => {
        console.error('Failed to search jobs', err);
        this.loading.set(false);
        this.notificationService.show({
          type: 'warning',
          title: 'Search Request',
          message: err.error?.error || 'Could not fetch jobs. Please check connection.'
        });
      }
    });
  }

  syncJobsNow(): void {
    if (this.syncing()) return;
    this.syncing.set(true);

    this.notificationService.show({
      type: 'job',
      title: 'Global Job Crawler Running 🌐',
      message: 'Scanning international boards (Arbeitnow, Remotive, WeWorkRemotely)...'
    });

    this.jobService.syncNow().subscribe({
      next: (res) => {
        this.syncing.set(false);
        this.notificationService.show({
          type: 'success',
          title: 'Crawling Completed 🚀',
          message: res.message || `Sync completed. ${res.newJobsCreated} new jobs ingested.`
        });
        this.loadJobs();
      },
      error: (err) => {
        this.syncing.set(false);
        this.notificationService.show({
          type: 'warning',
          title: 'Crawler Message',
          message: err.error?.message || 'Sync operation encountered an issue or timed out.'
        });
      }
    });
  }

  applyForJob(job: JobSearchResultDto): void {
    if (job.externalApplyUrl) {
      window.open(job.externalApplyUrl, '_blank');
    } else {
      this.jobService.applyToJob(job.id).subscribe({
        next: () => {
          this.notificationService.show({
            type: 'success',
            title: 'Application Submitted!',
            message: `Your application for ${job.title} at ${job.companyName} was received.`
          });
        },
        error: (err) => {
          if (err.status === 401) {
            this.notificationService.show({
              type: 'warning',
              title: 'Login Required',
              message: 'Please sign in or register to submit job applications.'
            });
            this.router.navigate(['/login']);
          } else {
            this.notificationService.show({
              type: 'warning',
              title: 'Application Error',
              message: err.error?.error || 'Failed to submit application.'
            });
          }
        }
      });
    }
  }

  getEmploymentLabel(val: number): string {
    return this.employmentTypes.find(t => t.value === val)?.label ?? '';
  }

  getExperienceLabel(val: number): string {
    return this.experienceLevels.find(t => t.value === val)?.label ?? '';
  }

  formatSalary(min?: number, max?: number, currency = 'USD'): string {
    if (min == null && max == null) return '';
    const currSym = currency === 'EUR' ? '€' : currency === 'GBP' ? '£' : '$';
    if (min != null && max != null) {
      return `${currSym}${(min / 1000).toFixed(0)}k - ${currSym}${(max / 1000).toFixed(0)}k`;
    }
    if (min != null) return `From ${currSym}${(min / 1000).toFixed(0)}k`;
    if (max != null) return `Up to ${currSym}${(max / 1000).toFixed(0)}k`;
    return '';
  }

  logout() {
    this.authService.logout().subscribe({
      next: () => this.router.navigate(['/login']),
      error: () => this.authService.logoutLocal()
    });
  }
}
