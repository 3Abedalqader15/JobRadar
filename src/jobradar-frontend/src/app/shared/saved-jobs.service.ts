import { Injectable, signal, inject, effect, untracked } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../auth/auth.service';

const STORAGE_KEY = 'jobradar_saved_jobs';

@Injectable({
  providedIn: 'root'
})
export class SavedJobsService {
  private http = inject(HttpClient);
  private authService = inject(AuthService);

  public savedJobIds = signal<Set<string>>(this.loadInitialSavedJobs());
  private syncedForUserId: string | null = null;

  constructor() {
    // Automatically trigger batch merge only once whenever the user logs in
    effect(() => {
      const user = this.authService.currentUserSignal();
      const currentId = user ? (user.userId || user.email) : null;
      if (currentId && currentId !== this.syncedForUserId) {
        this.syncedForUserId = currentId;
        untracked(() => {
          this.syncBatchOnLogin();
        });
      } else if (!currentId) {
        this.syncedForUserId = null;
      }
    });
  }

  private loadInitialSavedJobs(): Set<string> {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        const parsed = JSON.parse(stored);
        if (Array.isArray(parsed)) {
          return new Set<string>(parsed);
        }
      }
    } catch {
      // Fallback
    }
    return new Set<string>();
  }

  private persistLocal(): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(Array.from(this.savedJobIds())));
    } catch {
      // Storage unavailable
    }
  }

  public isSaved(jobId: string): boolean {
    return this.savedJobIds().has(jobId);
  }

  public toggleSave(jobId: string): void {
    const current = new Set(this.savedJobIds());
    const wasSaved = current.has(jobId);

    if (wasSaved) {
      current.delete(jobId);
    } else {
      current.add(jobId);
    }

    this.savedJobIds.set(current);
    this.persistLocal();

    // If authenticated, sync with backend
    if (this.authService.currentUserSignal()) {
      if (wasSaved) {
        this.http.delete(`/api/jobs/${jobId}/save`).subscribe({
          error: (err) => console.error('Failed to unsave job on server', err)
        });
      } else {
        this.http.post(`/api/jobs/${jobId}/save`, {}).subscribe({
          error: (err) => console.error('Failed to save job on server', err)
        });
      }
    }
  }

  public syncBatchOnLogin(): void {
    const localIds = untracked(() => Array.from(this.savedJobIds()));
    this.http.post<string[]>('/api/jobs/saved/batch', { jobIds: localIds }).subscribe({
      next: (serverIds) => {
        if (Array.isArray(serverIds)) {
          const merged = new Set<string>(serverIds);
          this.savedJobIds.set(merged);
          this.persistLocal();
        }
      },
      error: (err) => console.error('Failed to batch sync saved jobs on login', err)
    });
  }
}
