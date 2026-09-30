import { Component, OnInit, OnDestroy, effect, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { Subscription } from 'rxjs';
import { JobFeedService } from './job-feed.service';
import { JobSearchResultDto } from './job.model';

@Component({
  selector: 'app-job-feed',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './job-feed.component.html',
  styleUrls: ['./job-feed.component.css']
})
export class JobFeedComponent implements OnInit, OnDestroy {
  filterForm: FormGroup;
  
  // Using Angular 17 Signals for reactive state
  jobs = signal<JobSearchResultDto[]>([]);
  totalCount = signal<number>(0);
  loading = signal<boolean>(false);
  
  private formSub!: Subscription;
  
  // Filter Options
  employmentTypes = [
    { value: 0, label: 'Full Time' },
    { value: 1, label: 'Part Time' },
    { value: 2, label: 'Contract' }
  ];
  
  experienceLevels = [
    { value: 0, label: 'Junior' },
    { value: 1, label: 'Mid' },
    { value: 2, label: 'Senior' }
  ];

  constructor(private fb: FormBuilder, private jobService: JobFeedService) {
    this.filterForm = this.fb.group({
      query: ['Developer'],
      location: [''],
      employmentType: [null],
      experienceLevel: [null],
      skills: [[]] // Handled client-side or sent to API depending on backend support
    });

    // Reactive Effect to handle real-time jobs from SignalR
    effect(() => {
      const newJob = this.jobService.newJobSignal();
      if (newJob) {
        // Prepend the new job to the list without full page refresh
        this.jobs.update(currentJobs => [newJob, ...currentJobs]);
        this.totalCount.update(count => count + 1);
      }
    }, { allowSignalWrites: true });
  }

  ngOnInit(): void {
    // 1. Connect to Real-time Hub
    this.jobService.startSignalRConnection();
    
    // 2. Initial Data Load
    this.loadJobs();

    // 3. Listen to Form Changes (Debounced API calls)
    this.formSub = this.filterForm.valueChanges
      .pipe(
        debounceTime(400),
        distinctUntilChanged((prev, curr) => JSON.stringify(prev) === JSON.stringify(curr))
      )
      .subscribe(() => {
        this.loadJobs();
      });
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
      // Call internal API
      this.jobService.applyToJob(job.id).subscribe({
        next: (res) => alert(`Successfully applied for ${job.title} at ${job.companyName}!`),
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
}
