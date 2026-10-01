import { Component, OnInit, OnDestroy, effect, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { Subscription } from 'rxjs';
import { JobFeedService } from './job-feed.service';
import { JobSearchResultDto } from './job.model';
import { AuthService } from '../auth/auth.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-job-feed',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  templateUrl: './job-feed.component.html',
  styleUrls: ['./job-feed.component.css']
})
export class JobFeedComponent implements OnInit, OnDestroy {
  filterForm: FormGroup;

  jobs = signal<JobSearchResultDto[]>([]);
  totalCount = signal<number>(0);
  loading = signal<boolean>(false);

  private formSub!: Subscription;

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
    private fb: FormBuilder,
    private jobService: JobFeedService,
    public authService: AuthService,
    private router: Router
  ) {
    this.filterForm = this.fb.group({
      query: ['Developer'],
      location: [''],
      employmentType: [null],
      experienceLevel: [null],
      skills: [[]]
    });

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
        debounceTime(400),
        distinctUntilChanged((prev, curr) => JSON.stringify(prev) === JSON.stringify(curr))
      )
      .subscribe(() => this.loadJobs());
  }

  ngOnDestroy(): void {
    this.formSub?.unsubscribe();
    this.jobService.stopSignalRConnection();
  }

  loadJobs(): void {
    this.loading.set(true);
    const filters = this.filterForm.value;

    const payload = {
      query: (filters.query && filters.query.trim()) ? filters.query.trim() : 'Developer',
      location: filters.location || null,
      employmentType: filters.employmentType !== 'null' ? Number(filters.employmentType) : null,
      experienceLevel: filters.experienceLevel !== 'null' ? Number(filters.experienceLevel) : null,
      page: 1,
      pageSize: 20
    };

    if (isNaN(payload.employmentType as number)) payload.employmentType = null;
    if (isNaN(payload.experienceLevel as number)) payload.experienceLevel = null;

    this.jobService.searchJobs(payload).subscribe({
      next: (res) => {
        this.jobs.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to search jobs', err);
        this.loading.set(false);
      }
    });
  }

  applyForJob(job: JobSearchResultDto): void {
    if (job.externalApplyUrl) {
      window.open(job.externalApplyUrl, '_blank');
    } else {
      this.jobService.applyToJob(job.id).subscribe({
        next: () => alert(`Successfully applied for ${job.title} at ${job.companyName}!`),
        error: (err) => {
          if (err.status === 401) {
            alert('Please login to apply for this job.');
          } else if (err.error?.error) {
            alert(err.error.error);
          } else {
            alert('Failed to apply. Please try again later.');
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

  logout() {
    this.authService.logout().subscribe({
      next: () => this.router.navigate(['/login']),
      error: () => this.authService.logoutLocal()
    });
  }
}
