import {
  Component,
  OnInit,
  Input,
  Output,
  EventEmitter,
  signal,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JobFeedService } from './job-feed.service';
import { JobSearchResultDto, JobQuestionDto, QuestionType } from './job.model';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-job-apply-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './job-apply-modal.component.html',
  styleUrls: ['./job-apply-modal.component.css']
})
export class JobApplyModalComponent implements OnInit {
  @Input({ required: true }) job!: JobSearchResultDto;
  @Output() closed = new EventEmitter<void>();
  @Output() applied = new EventEmitter<{ applicationId: string; job: JobSearchResultDto }>();

  private jobService = inject(JobFeedService);
  private authService = inject(AuthService);

  QuestionType = QuestionType;

  // Contact fields
  fullName = '';
  email = '';
  phone = '';

  // CV dropzone state
  selectedFile: File | null = null;
  fileError: string | null = null;
  isDragging = false;

  // Questions state
  questions = signal<JobQuestionDto[]>([]);
  questionsLoading = signal(true);
  answers: Record<string, string> = {};

  // Form submission state
  submitting = signal(false);
  submitError = signal<string | null>(null);
  submitSuccess = signal(false);

  ngOnInit(): void {
    // Pre-fill contact details from current user profile
    const user = this.authService.currentUserSignal();
    if (user) {
      this.fullName = user.fullName || '';
      this.email = user.email || '';
    }

    this.loadQuestions();
  }

  loadQuestions(): void {
    this.questionsLoading.set(true);
    this.jobService.getJobQuestions(this.job.id).subscribe({
      next: (qList) => {
        this.questions.set(qList || []);
        // Initialize answers dictionary
        (qList || []).forEach(q => {
          if (q.id) {
            this.answers[q.id] = '';
          }
        });
        this.questionsLoading.set(false);
      },
      error: () => {
        // If questions endpoint fails or has none, continue gracefully
        this.questions.set([]);
        this.questionsLoading.set(false);
      }
    });
  }

  // ── CV File Dropzone & Validation ────────────────────────────────
  onDragOver(e: DragEvent): void {
    e.preventDefault();
    e.stopPropagation();
    this.isDragging = true;
  }

  onDragLeave(e: DragEvent): void {
    e.preventDefault();
    e.stopPropagation();
    this.isDragging = false;
  }

  onDrop(e: DragEvent): void {
    e.preventDefault();
    e.stopPropagation();
    this.isDragging = false;

    const files = e.dataTransfer?.files;
    if (files && files.length > 0) {
      this.validateAndSetFile(files[0]);
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.validateAndSetFile(input.files[0]);
    }
  }

  validateAndSetFile(file: File): void {
    this.fileError = null;

    const name = file.name.toLowerCase();
    const sizeInMb = file.size / (1024 * 1024);

    // 1. Explicit check and rejection for legacy .doc binary format
    if (name.endsWith('.doc') && !name.endsWith('.docx')) {
      this.fileError = 'Legacy .doc binary format is not supported; please upload a .pdf or .docx document.';
      this.selectedFile = null;
      return;
    }

    // 2. Format extension check (.pdf, .docx only)
    if (!name.endsWith('.pdf') && !name.endsWith('.docx')) {
      this.fileError = 'Invalid file type. Only PDF (.pdf) and Word (.docx) documents are accepted.';
      this.selectedFile = null;
      return;
    }

    // 3. File size check (Max 5MB)
    const maxBytes = 5 * 1024 * 1024;
    if (file.size > maxBytes) {
      this.fileError = `File size (${sizeInMb.toFixed(1)} MB) exceeds the 5 MB maximum limit.`;
      this.selectedFile = null;
      return;
    }

    if (file.size === 0) {
      this.fileError = 'The selected file is empty.';
      this.selectedFile = null;
      return;
    }

    this.selectedFile = file;
  }

  removeFile(): void {
    this.selectedFile = null;
    this.fileError = null;
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }

  // ── Submission ──────────────────────────────────────────────────
  submitApplication(): void {
    this.submitError.set(null);

    // Validate Contact Details
    if (!this.fullName.trim()) {
      this.submitError.set('Please provide your full name.');
      return;
    }

    if (!this.email.trim() || !this.email.includes('@')) {
      this.submitError.set('Please provide a valid email address.');
      return;
    }

    if (!this.phone.trim()) {
      this.submitError.set('Please provide your phone number.');
      return;
    }

    // Validate CV upload
    if (!this.selectedFile) {
      this.submitError.set('Please upload your CV (.pdf or .docx format).');
      return;
    }

    // Validate Required Screening Questions
    const missingTitles: string[] = [];
    for (const q of this.questions()) {
      if (q.isRequired && q.id) {
        const val = this.answers[q.id]?.trim();
        if (!val) {
          missingTitles.push(`"${q.questionText}"`);
        }
      }
    }

    if (missingTitles.length > 0) {
      this.submitError.set(`Please answer all required screening questions: ${missingTitles.join(', ')}`);
      return;
    }

    this.submitting.set(true);

    const formData = new FormData();
    formData.append('ApplicantFullName', this.fullName.trim());
    formData.append('ApplicantEmail', this.email.trim());
    formData.append('ApplicantPhone', this.phone.trim());
    formData.append('CvFile', this.selectedFile, this.selectedFile.name);

    // Format answers array
    const answersPayload = this.questions()
      .filter(q => q.id && this.answers[q.id]?.trim())
      .map(q => ({
        QuestionId: q.id,
        AnswerText: this.answers[q.id!].trim()
      }));

    formData.append('Answers', JSON.stringify(answersPayload));

    this.jobService.submitJobApplication(this.job.id, formData).subscribe({
      next: (res) => {
        this.submitting.set(false);
        this.submitSuccess.set(true);
        setTimeout(() => {
          this.applied.emit({ applicationId: res.applicationId, job: this.job });
          this.closeModal();
        }, 1500);
      },
      error: (err) => {
        this.submitting.set(false);
        const msg = err.error?.error || err.error?.message || err.error?.detail || err.error?.title || 'Failed to submit application.';
        this.submitError.set(msg);
      }
    });
  }

  closeModal(): void {
    this.closed.emit();
  }
}
