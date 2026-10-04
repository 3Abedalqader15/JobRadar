import {
  Component,
  OnInit,
  OnDestroy,
  AfterViewInit,
  effect,
  signal,
  inject,
  computed,
  HostListener,
  ElementRef,
  ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { Subscription } from 'rxjs';
import { Map as MapLibreMap, Marker, NavigationControl } from 'maplibre-gl';
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
import { ThemeService } from '../theme.service';
import { PaginationComponent } from '../shared/pagination.component';
import { RadarChartComponent, RadarMetrics } from '../shared/radar-chart.component';
import { SwipeCardComponent } from './swipe-card.component';
import { SavedJobsService } from '../shared/saved-jobs.service';

export interface CategoryShortcut {
  id: string;
  label: string;
  icon: string;
  query?: string;
  isRemote?: boolean;
}

const CITY_COORDINATES: Record<string, [number, number]> = {
  amman: [35.9284, 31.9539],
  irbid: [35.85, 32.5556],
  zarqa: [36.088, 32.0728],
  aqaba: [35.0063, 29.5267],
  salt: [35.7272, 32.0392],
  madaba: [35.7939, 31.7197],
  jerash: [35.8961, 32.2808],
  karak: [35.7056, 31.1853],
  mafraq: [36.2081, 32.3442],
  dubai: [55.2708, 25.2048],
  riyadh: [46.6753, 24.7136],
  cairo: [31.2357, 30.0444],
  doha: [51.531, 25.2854],
  kuwait: [47.9774, 29.3759],
  'abu dhabi': [54.3773, 24.4539],
  remote: [35.9284, 31.9539]
};

@Component({
  selector: 'app-job-feed',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    PaginationComponent,
    RadarChartComponent,
    SwipeCardComponent
  ],
  templateUrl: './job-feed.component.html',
  styleUrls: ['./job-feed.component.css']
})
export class JobFeedComponent implements OnInit, OnDestroy, AfterViewInit {
  private fb = inject(FormBuilder);
  public jobService = inject(JobFeedService);
  public authService = inject(AuthService);
  public themeService = inject(ThemeService);
  public savedJobsService = inject(SavedJobsService);
  private notificationService = inject(NotificationService);
  private router = inject(Router);

  @ViewChild('mapContainer') mapContainer?: ElementRef<HTMLDivElement>;

  filterForm: FormGroup;

  // Primary Feed State
  jobs = signal<JobSearchResultDto[]>([]);
  totalCount = signal<number>(0);
  loading = signal<boolean>(false);
  syncing = signal<boolean>(false);
  searchLatencyMs = signal<number | null>(null);

  // Saved Jobs Filter & Swipe Mode Triage
  savedOnlyFilter = signal<boolean>(false);
  swipeModeOpen = signal<boolean>(false);

  filteredJobs = computed(() => {
    let list = this.jobs();

    // Saved-only filter
    if (this.savedOnlyFilter()) {
      const saved = this.savedJobsService.savedJobIds();
      list = list.filter(j => saved.has(j.id));
    }

    // Skill filter — client-side to avoid backend API errors
    const skills = this.selectedSkills();
    if (skills.length > 0) {
      list = list.filter(j =>
        j.skills?.some(s =>
          skills.some(sel => sel.toLowerCase() === s.toLowerCase())
        )
      );
    }

    return list;
  });

  // View Mode: 'list' (default dense scannable), 'grid', 'map' (Radar Pulse)
  viewMode = signal<'list' | 'grid' | 'map'>('list');

  // Mobile Filter Drawer
  mobileFiltersOpen = signal<boolean>(false);

  // Pagination State
  currentPage = signal<number>(1);
  pageSize = signal<number>(24);

  // Split-View Quick Drawer State
  selectedJob = signal<JobSearchResultDto | null>(null);
  drawerJobDetail = signal<any | null>(null);
  loadingDetail = signal<boolean>(false);
  drawerOpen = computed(() => this.selectedJob() !== null);

  // Map Selected Job for Quick Preview on Radar Pulse Map
  mapSelectedJob = signal<JobSearchResultDto | null>(null);

  // Category Quick-Browse Strip
  selectedCategory = signal<string | null>(null);
  categories: CategoryShortcut[] = [
    { id: 'tech', label: 'Tech & Engineering', icon: '💻', query: 'Developer Software Engineer Cloud' },
    { id: 'remote', label: 'Remote Only', icon: '🌍', isRemote: true },
    { id: 'healthcare', label: 'Healthcare & Biotech', icon: '🏥', query: 'Health Medical Clinical Pharmacy' },
    { id: 'engineering', label: 'Civil & Mechanical', icon: '⚙️', query: 'Civil Mechanical Electrical Engineer' },
    { id: 'finance', label: 'Banking & Fintech', icon: '📈', query: 'Finance Accounting Fintech Banking' },
    { id: 'marketing', label: 'Sales & Growth', icon: '🚀', query: 'Marketing Sales Business Development' }
  ];

  // Popular skills cloud for 1-click filtering
  popularSkills: string[] = ['.NET', 'C#', 'Angular', 'TypeScript', 'PostgreSQL', 'Docker', 'Python', 'React', 'AWS', 'Kubernetes'];
  selectedSkills = signal<string[]>([]);

  // Filter Select Options
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

  // Active filter count for mobile badge
  activeFilterCount = computed(() => {
    let count = this.selectedSkills().length;
    const f = this.filterForm?.value;
    if (!f) return count;
    if (f.query?.trim()) count++;
    if (f.location?.trim()) count++;
    if (f.isRemote) count++;
    if (f.employmentType != null && f.employmentType !== 'null') count++;
    if (f.experienceLevel != null && f.experienceLevel !== 'null') count++;
    if (f.salaryMin || f.salaryMax) count++;
    if (f.datePosted && Number(f.datePosted) !== DatePostedFilter.AllTime) count++;
    return count;
  });

  private formSub!: Subscription;
  private map: MapLibreMap | null = null;
  private markers: Marker[] = [];

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
        if (this.viewMode() === 'map') {
          this.pulseJobOnRadar(newJob);
        }
      }
    }, { allowSignalWrites: true });

    // Reactive effect for theme changes to update map style
    effect(() => {
      const theme = this.themeService.currentTheme();
      if (this.map) {
        this.updateMapTheme(theme);
      }
    });
  }

  ngOnInit(): void {
    this.jobService.startSignalRConnection();
    this.loadJobs();

    this.formSub = this.filterForm.valueChanges
      .pipe(
        debounceTime(350),
        distinctUntilChanged((prev, curr) => JSON.stringify(prev) === JSON.stringify(curr))
      )
      .subscribe(() => {
        this.currentPage.set(1);
        this.loadJobs();
      });
  }

  ngAfterViewInit(): void {
    if (this.viewMode() === 'map') {
      this.initOrResizeMap();
    }
  }

  ngOnDestroy(): void {
    this.formSub?.unsubscribe();
    this.jobService.stopSignalRConnection();
    this.destroyMap();
  }

  // View Mode Switching
  setViewMode(mode: 'list' | 'grid' | 'map'): void {
    this.viewMode.set(mode);
    if (mode === 'map') {
      setTimeout(() => this.initOrResizeMap(), 60);
    }
  }

  // Quick Category Strip Toggle
  selectCategory(cat: CategoryShortcut): void {
    if (this.selectedCategory() === cat.id) {
      this.selectedCategory.set(null);
      if (cat.isRemote) {
        this.filterForm.patchValue({ isRemote: false });
      } else {
        this.filterForm.patchValue({ query: '' });
      }
    } else {
      this.selectedCategory.set(cat.id);
      if (cat.isRemote) {
        this.filterForm.patchValue({ isRemote: true });
      } else {
        this.filterForm.patchValue({ query: cat.query || '' });
      }
    }
  }

  // Toggle Saved Jobs Only View
  toggleSavedOnly(): void {
    this.savedOnlyFilter.set(!this.savedOnlyFilter());
  }

  // Compute 5-axis Radar Metrics from authentic job parameters
  getRadarMetrics(job: JobSearchResultDto): RadarMetrics {
    // 1. Skills Overlap: calculated from candidate selected skills or base relevance
    let skillsFit = Math.round((job.relevanceScore || 0.75) * 100);
    if (job.skills && job.skills.length > 0) {
      const selected = this.selectedSkills();
      if (selected.length > 0) {
        const matched = job.skills.filter(s => selected.some(sel => sel.toLowerCase() === s.toLowerCase()));
        skillsFit = Math.round(50 + (matched.length / Math.max(1, selected.length)) * 50);
      }
    }
    skillsFit = Math.max(35, Math.min(98, skillsFit));

    // 2. Experience Fit: based on experienceLevel
    let expFit = 75;
    if (job.experienceLevel === ExperienceLevel.EntryLevel) expFit = 85;
    else if (job.experienceLevel === ExperienceLevel.MidLevel) expFit = 90;
    else if (job.experienceLevel === ExperienceLevel.Senior) expFit = 82;
    else if (job.experienceLevel === ExperienceLevel.Lead) expFit = 72;

    // 3. Location Fit: remote jobs receive high fit; regional location matches benchmarked
    let locFit = job.isRemote ? 95 : 80;
    const filterLoc = this.filterForm.get('location')?.value?.toLowerCase();
    if (filterLoc && job.location && job.location.toLowerCase().includes(filterLoc)) {
      locFit = 98;
    }

    // 4. Seniority Fit:
    let senFit = 78;
    if (job.experienceLevel) {
      senFit = 68 + (job.experienceLevel * 7);
    }

    // 5. Workplace / Remote Fit:
    const prefRemote = this.filterForm.get('isRemote')?.value;
    let workplaceFit = 85;
    if (prefRemote) {
      workplaceFit = job.isRemote ? 100 : 55;
    } else {
      workplaceFit = job.isRemote ? 92 : 86;
    }

    return {
      skills: skillsFit,
      experience: expFit,
      location: locFit,
      seniority: senFit,
      workplace: workplaceFit
    };
  }

  // Freshness Decay Indicator (Pure client-side calculation from postedAt + applicantsClickCount)
  getFreshness(job: JobSearchResultDto): { pct: number; label: string; tone: 'fresh' | 'steady' | 'aging' } {
    const postedTime = job.postedAt ? new Date(job.postedAt).getTime() : Date.now();
    const ageHours = Math.max(0, (Date.now() - postedTime) / (1000 * 60 * 60));
    const clicks = job.applicantsClickCount || 0;

    // Decay formula: 100 base, decays with time (~1.2% per hour) and competition (~2.5% per click)
    let score = 100 - (ageHours * 1.2) - (clicks * 2.5);
    score = Math.max(10, Math.min(100, Math.round(score)));

    if (score >= 75) {
      return { pct: score, label: 'Fresh Radar Signal', tone: 'fresh' };
    } else if (score >= 45) {
      return { pct: score, label: 'Active Opportunity', tone: 'steady' };
    } else {
      return { pct: score, label: 'High Competition', tone: 'aging' };
    }
  }

  // Live Applicant Counter: ONLY shown when supported by real applicantsClickCount >= 3
  hasRealApplicants(job: JobSearchResultDto): boolean {
    return (job.applicantsClickCount ?? 0) >= 3;
  }

  getApplicantCountText(job: JobSearchResultDto): string {
    const count = job.applicantsClickCount || 0;
    return `${count} applied recently`;
  }

  // Relative Time Display
  getRelativeTime(postedAt?: string): string {
    if (!postedAt) return 'Recent';
    const diffMs = Date.now() - new Date(postedAt).getTime();
    const diffMins = Math.floor(diffMs / 60000);
    if (diffMins < 60) return `${Math.max(1, diffMins)}m ago`;
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    const diffDays = Math.floor(diffHours / 24);
    if (diffDays === 1) return 'Yesterday';
    if (diffDays < 30) return `${diffDays}d ago`;
    return `${Math.floor(diffDays / 30)}mo ago`;
  }

  /**
   * Converts a raw job description (flat text with inline bullet • separators
   * and section headings) into a structured array of tokens for the template.
   * Each token is: { type: 'heading' | 'bullet' | 'paragraph', text: string }
   */
  formatDescription(raw: string): Array<{ type: 'heading' | 'bullet' | 'paragraph'; text: string }> {
    if (!raw) return [];

    // Section heading keywords that often appear without a bullet
    const headingPattern = /^(What You[''']ll Do|What We[''']re Looking For|Requirements|Responsibilities|Qualifications|About (the Role|Us|You)|What Success Looks Like|Benefits|Nice to Have|Who You Are)[:\s]*/i;

    // Split on bullet character OR newline
    const rawParts = raw.split(/(?=[•●▪])| *\n+ */g);
    const tokens: Array<{ type: 'heading' | 'bullet' | 'paragraph'; text: string }> = [];

    for (let part of rawParts) {
      // Strip leading bullet symbols and trim
      const clean = part.replace(/^[•●▪]\s*/, '').trim();
      if (!clean) continue;

      if (headingPattern.test(clean)) {
        // Remove trailing colon from heading
        tokens.push({ type: 'heading', text: clean.replace(/:$/, '').trim() });
      } else if (part.trimStart().startsWith('•') || part.trimStart().startsWith('●') || part.trimStart().startsWith('▪')) {
        tokens.push({ type: 'bullet', text: clean });
      } else if (clean.endsWith(':') && clean.length < 80) {
        tokens.push({ type: 'heading', text: clean.replace(/:$/, '').trim() });
      } else {
        tokens.push({ type: 'paragraph', text: clean });
      }
    }

    return tokens;
  }

  // Pagination Handlers
  onPageChange(page: number): void {
    this.currentPage.set(page);
    this.loadJobs();
    const feedHeader = document.querySelector('.feed-top-bar');
    if (feedHeader) {
      feedHeader.scrollIntoView({ behavior: 'smooth' });
    }
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.currentPage.set(1);
    this.loadJobs();
  }

  // Drawer Handlers
  openJobDrawer(job: JobSearchResultDto): void {
    this.selectedJob.set(job);
    this.drawerJobDetail.set(null);
    this.loadingDetail.set(true);

    this.jobService.getJobDetail(job.id).subscribe({
      next: (detail) => {
        this.drawerJobDetail.set(detail);
        this.loadingDetail.set(false);
      },
      error: () => {
        this.loadingDetail.set(false);
      }
    });
  }

  closeJobDrawer(): void {
    this.selectedJob.set(null);
    this.drawerJobDetail.set(null);
  }

  @HostListener('document:keydown.escape')
  handleEscape(): void {
    if (this.drawerOpen()) {
      this.closeJobDrawer();
    }
    if (this.mobileFiltersOpen()) {
      this.mobileFiltersOpen.set(false);
    }
    if (this.mapSelectedJob()) {
      this.mapSelectedJob.set(null);
    }
  }

  // Skill Chip Interactions
  onSkillChipClick(skill: string, event: MouseEvent): void {
    event.stopPropagation();
    this.toggleSkill(skill);
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
    this.currentPage.set(1);
    this.loadJobs();
  }

  // Filter Presets & Reset
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
    this.selectedCategory.set(null);
    this.currentPage.set(1);
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

  // Core Data Loading
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
      // Skills are filtered client-side in filteredJobs() to avoid backend API errors
      datePosted: Number(formVals.datePosted) || DatePostedFilter.AllTime,
      sortBy: Number(formVals.sortBy) || JobSortOption.Relevance,
      page: this.currentPage(),
      pageSize: this.pageSize()
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
        this.searchLatencyMs.set(
          res.items.length > 0 && res.items[0].searchDurationMs != null
            ? res.items[0].searchDurationMs
            : duration
        );
        if (this.viewMode() === 'map' && this.map) {
          this.refreshMapMarkers();
        }
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

  // Crawler Ingestion
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

  // Job Application
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

  // Label and Formatting Helpers
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

  logout(): void {
    this.authService.logout().subscribe({
      next: () => this.router.navigate(['/login']),
      error: () => this.authService.logoutLocal()
    });
  }

  // ══════════════════════════════════════════════════════════════════
  // RADAR PULSE MAP (MapLibre GL JS — Jordan & MENA Focus)
  // ══════════════════════════════════════════════════════════════════

  private initOrResizeMap(): void {
    if (!this.mapContainer?.nativeElement) return;

    if (this.map) {
      this.map.resize();
      this.refreshMapMarkers();
      return;
    }

    const isDark = this.themeService.currentTheme() === 'dark';
    const tileUrl = isDark
      ? 'https://a.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png'
      : 'https://a.basemaps.cartocdn.com/light_all/{z}/{x}/{y}.png';

    this.map = new MapLibreMap({
      container: this.mapContainer.nativeElement,
      style: {
        version: 8,
        sources: {
          'carto-tiles': {
            type: 'raster',
            tiles: [tileUrl],
            tileSize: 256,
            attribution: '&copy; OpenStreetMap contributors &copy; CARTO'
          }
        },
        layers: [
          {
            id: 'carto-base-layer',
            type: 'raster',
            source: 'carto-tiles',
            minzoom: 0,
            maxzoom: 19
          }
        ]
      },
      center: [35.9284, 31.9539], // Centered on Jordan (Amman)
      zoom: 6.8,
      minZoom: 4,
      maxZoom: 16
    });

    this.map.addControl(new NavigationControl({ showCompass: true }), 'top-right');

    this.map.on('load', () => {
      this.refreshMapMarkers();
    });

    this.map.on('click', (e) => {
      // If clicking bare map, close preview
      const target = e.originalEvent.target as HTMLElement;
      if (!target.closest('.radar-blip-element')) {
        this.mapSelectedJob.set(null);
      }
    });
  }

  private updateMapTheme(theme: 'light' | 'dark'): void {
    if (!this.map) return;
    const tileUrl = theme === 'dark'
      ? 'https://a.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png'
      : 'https://a.basemaps.cartocdn.com/light_all/{z}/{x}/{y}.png';

    const source = this.map.getSource('carto-tiles') as any;
    if (source && source.setTiles) {
      source.setTiles([tileUrl]);
    }
  }

  private refreshMapMarkers(): void {
    if (!this.map) return;

    // Clear previous markers
    for (const marker of this.markers) {
      marker.remove();
    }
    this.markers = [];

    const jobList = this.jobs();
    for (const job of jobList) {
      const coords = this.getJobCoordinates(job);
      const markerEl = this.createRadarMarkerElement(job, false);

      const marker = new Marker({ element: markerEl, anchor: 'center' })
        .setLngLat(coords)
        .addTo(this.map);

      this.markers.push(marker);
    }
  }

  private pulseJobOnRadar(job: JobSearchResultDto): void {
    if (!this.map) return;
    const coords = this.getJobCoordinates(job);

    // Create live pulse marker with amber signal
    const pulseEl = this.createRadarMarkerElement(job, true);
    const marker = new Marker({ element: pulseEl, anchor: 'center' })
      .setLngLat(coords)
      .addTo(this.map);

    this.markers.unshift(marker);

    // Pan smoothly to the newly ingested job
    this.map.flyTo({
      center: coords,
      zoom: Math.max(this.map.getZoom(), 8),
      speed: 1.2
    });

    this.mapSelectedJob.set(job);
  }

  private createRadarMarkerElement(job: JobSearchResultDto, isLivePulse: boolean): HTMLElement {
    const el = document.createElement('div');
    el.className = `radar-blip-element ${isLivePulse ? 'is-live-pulse' : ''} ${job.isVerified ? 'is-verified-blip' : ''}`;
    el.title = `${job.title} — ${job.companyName}`;

    el.innerHTML = `
      <div class="blip-ring"></div>
      <div class="blip-core"></div>
    `;

    el.addEventListener('click', (e) => {
      e.stopPropagation();
      this.mapSelectedJob.set(job);
    });

    return el;
  }

  getJobCoordinates(job: JobSearchResultDto): [number, number] {
    const loc = (job.location || '').toLowerCase();
    let baseCoords: [number, number] = CITY_COORDINATES['amman'];

    for (const [city, coords] of Object.entries(CITY_COORDINATES)) {
      if (loc.includes(city)) {
        baseCoords = coords;
        break;
      }
    }

    if (job.isRemote && !job.location) {
      baseCoords = CITY_COORDINATES['amman'];
    }

    // Deterministic jitter to prevent identical coordinates from overlapping completely
    const hash = this.hashString(job.id || job.title);
    const jitterLng = ((hash % 100) - 50) * 0.0018;
    const jitterLat = (((hash >> 4) % 100) - 50) * 0.0018;

    return [baseCoords[0] + jitterLng, baseCoords[1] + jitterLat];
  }

  private hashString(str: string): number {
    let hash = 0;
    for (let i = 0; i < str.length; i++) {
      hash = (hash << 5) - hash + str.charCodeAt(i);
      hash |= 0;
    }
    return Math.abs(hash);
  }

  private destroyMap(): void {
    if (this.map) {
      for (const m of this.markers) {
        m.remove();
      }
      this.markers = [];
      this.map.remove();
      this.map = null;
    }
  }
}
