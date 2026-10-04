import {
  Component,
  Input,
  Output,
  EventEmitter,
  signal,
  computed,
  HostListener
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { JobSearchResultDto } from './job.model';
import { SavedJobsService } from '../shared/saved-jobs.service';
import { RadarChartComponent, RadarMetrics } from '../shared/radar-chart.component';

@Component({
  selector: 'app-swipe-card',
  standalone: true,
  imports: [CommonModule, RadarChartComponent],
  templateUrl: './swipe-card.component.html',
  styleUrls: ['./swipe-card.component.css']
})
export class SwipeCardComponent {
  @Input() jobs: JobSearchResultDto[] = [];
  @Output() viewDetails = new EventEmitter<JobSearchResultDto>();
  @Output() closeSwipe = new EventEmitter<void>();

  currentIndex = signal<number>(0);

  // Drag interaction state
  isDragging = signal<boolean>(false);
  dragX = signal<number>(0);
  dragY = signal<number>(0);
  private startX = 0;
  private startY = 0;

  // Swipe animation trigger
  animatingOut = signal<'left' | 'right' | null>(null);

  constructor(public savedJobsService: SavedJobsService) {}

  currentJob = computed<JobSearchResultDto | null>(() => {
    const list = this.jobs;
    const idx = this.currentIndex();
    return idx < list.length ? list[idx] : null;
  });

  nextJob = computed<JobSearchResultDto | null>(() => {
    const list = this.jobs;
    const idx = this.currentIndex() + 1;
    return idx < list.length ? list[idx] : null;
  });

  isDone = computed<boolean>(() => {
    return this.currentIndex() >= this.jobs.length;
  });

  // Calculate drag progress (-1 to +1)
  dragProgress = computed<number>(() => {
    const dx = this.dragX();
    const threshold = 140;
    return Math.max(-1, Math.min(1, dx / threshold));
  });

  // Compute transform style during drag
  cardTransform = computed<string>(() => {
    if (this.animatingOut() === 'right') {
      return 'translate3d(120vw, 30px, 0) rotate(25deg)';
    }
    if (this.animatingOut() === 'left') {
      return 'translate3d(-120vw, 30px, 0) rotate(-25deg)';
    }
    const dx = this.dragX();
    const dy = this.dragY() * 0.4;
    const rot = (dx / 18).toFixed(2);
    return `translate3d(${dx}px, ${dy}px, 0) rotate(${rot}deg)`;
  });

  // Touch Handlers
  onTouchStart(event: TouchEvent): void {
    if (this.animatingOut() || this.isDone()) return;
    const touch = event.touches[0];
    this.startX = touch.clientX;
    this.startY = touch.clientY;
    this.isDragging.set(true);
  }

  onTouchMove(event: TouchEvent): void {
    if (!this.isDragging()) return;
    const touch = event.touches[0];
    this.dragX.set(touch.clientX - this.startX);
    this.dragY.set(touch.clientY - this.startY);
  }

  onTouchEnd(): void {
    if (!this.isDragging()) return;
    this.isDragging.set(false);
    this.evaluateSwipeCommit();
  }

  // Mouse Handlers (For desktop responsiveness & pair testing)
  onMouseDown(event: MouseEvent): void {
    if (this.animatingOut() || this.isDone()) return;
    this.startX = event.clientX;
    this.startY = event.clientY;
    this.isDragging.set(true);
  }

  @HostListener('document:mousemove', ['$event'])
  onMouseMove(event: MouseEvent): void {
    if (!this.isDragging()) return;
    this.dragX.set(event.clientX - this.startX);
    this.dragY.set(event.clientY - this.startY);
  }

  @HostListener('document:mouseup')
  onMouseUp(): void {
    if (!this.isDragging()) return;
    this.isDragging.set(false);
    this.evaluateSwipeCommit();
  }

  private evaluateSwipeCommit(): void {
    const dx = this.dragX();
    const threshold = 90;
    if (dx > threshold) {
      this.swipeRight();
    } else if (dx < -threshold) {
      this.swipeLeft();
    } else {
      // Revert to center
      this.dragX.set(0);
      this.dragY.set(0);
    }
  }

  // User Actions
  swipeRight(): void {
    const job = this.currentJob();
    if (!job || this.animatingOut()) return;

    // Save job
    if (!this.savedJobsService.isSaved(job.id)) {
      this.savedJobsService.toggleSave(job.id);
    }

    this.animatingOut.set('right');
    setTimeout(() => {
      this.advanceCard();
    }, 280);
  }

  swipeLeft(): void {
    const job = this.currentJob();
    if (!job || this.animatingOut()) return;

    this.animatingOut.set('left');
    setTimeout(() => {
      this.advanceCard();
    }, 280);
  }

  private advanceCard(): void {
    this.currentIndex.update(i => i + 1);
    this.dragX.set(0);
    this.dragY.set(0);
    this.animatingOut.set(null);
  }

  resetStack(): void {
    this.currentIndex.set(0);
    this.dragX.set(0);
    this.dragY.set(0);
    this.animatingOut.set(null);
  }

  onDetailsClick(): void {
    const job = this.currentJob();
    if (job) {
      this.viewDetails.emit(job);
    }
  }

  getMetrics(job: JobSearchResultDto): RadarMetrics {
    return {
      skills: Math.round(50 + (job.relevanceScore || 0.7) * 45),
      experience: 80,
      location: job.isRemote ? 95 : 80,
      seniority: 78,
      workplace: job.isRemote ? 95 : 85
    };
  }
}
