import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { Observable } from 'rxjs';
import { JobSearchCriteriaDto, JobSearchResultDto, PagedResult, JobQuestionDto } from './job.model';
import { NotificationService } from '../notifications/notification.service';
import { computeDesiredGroups } from './job-feed.utils';

export type SignalRConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

@Injectable({
  providedIn: 'root'
})
export class JobFeedService {
  private http = inject(HttpClient);
  private notificationService = inject(NotificationService);

  private hubConnection: signalR.HubConnection | undefined;
  private currentSubscribedGroups = new Set<string>();

  // Reactive state signals for real-time updates and resilience
  public readonly newJobSignal = signal<JobSearchResultDto | null>(null);
  public readonly deactivatedJobSignal = signal<string | null>(null);
  public readonly connectionStateSignal = signal<SignalRConnectionState>('disconnected');
  public readonly reconnectedSignal = signal<number>(0);

  public startSignalRConnection(): void {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      return;
    }

    this.connectionStateSignal.set('connecting');

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/jobs')
      .withAutomaticReconnect()
      .build();

    this.hubConnection.onreconnecting(() => {
      console.warn('⚠️ SignalR connection lost, attempting automatic reconnect...');
      this.connectionStateSignal.set('reconnecting');
    });

    this.hubConnection.onreconnected(async (newConnectionId) => {
      console.log('✅ SignalR reconnected with connectionId:', newConnectionId);
      this.connectionStateSignal.set('connected');

      // Re-join active criteria groups since server-side group membership is lost on reconnect
      if (this.currentSubscribedGroups.size > 0 && this.hubConnection) {
        try {
          await this.hubConnection.invoke('UpdateCriteriaSubscription', [], Array.from(this.currentSubscribedGroups));
          console.log('Re-joined criteria groups after reconnect:', Array.from(this.currentSubscribedGroups));
        } catch (err) {
          console.warn('Failed to re-join criteria groups after reconnect', err);
        }
      }

      // Signal components to re-run REST search to recover events missed during disconnection
      this.reconnectedSignal.set(Date.now());
    });

    this.hubConnection.onclose(() => {
      this.connectionStateSignal.set('disconnected');
    });

    // 1. Relevance-scoped event listener
    this.hubConnection.on('ReceiveRelevantJob', (job: JobSearchResultDto) => {
      if (!job) return;
      job.isNew = true;
      this.newJobSignal.set(job);

      // Trigger glassmorphic toast notification
      const loc = job.isRemote ? 'Remote' : (job.location || 'Onsite');
      this.notificationService.show({
        type: 'job',
        title: 'New Matching Job ⚡',
        message: `${job.title} at ${job.companyName}`,
        jobId: job.id,
        actionUrl: job.externalApplyUrl,
        actionLabel: 'View',
        metaBadge: loc
      });
    });

    // 2. Job Deactivation event listener (broadcast to all)
    this.hubConnection.on('JobDeactivated', (payload: { id: string } | string) => {
      const id = typeof payload === 'string' ? payload : payload?.id;
      if (id) {
        this.deactivatedJobSignal.set(id);
      }
    });

    // Backward compatibility if global broadcast was invoked
    this.hubConnection.on('ReceiveNewJob', (job: JobSearchResultDto) => {
      if (!job) return;
      job.isNew = true;
      this.newJobSignal.set(job);
    });

    this.hubConnection
      .start()
      .then(async () => {
        console.log('✅ SignalR connection established to /hubs/jobs');
        this.connectionStateSignal.set('connected');

        // Initial default subscription: grp:all
        const initialGroups = this.currentSubscribedGroups.size > 0
          ? Array.from(this.currentSubscribedGroups)
          : ['grp:all'];

        try {
          await this.hubConnection?.invoke('UpdateCriteriaSubscription', [], initialGroups);
          this.currentSubscribedGroups = new Set(initialGroups);
        } catch (err) {
          console.warn('Initial group subscription error:', err);
        }
      })
      .catch(err => {
        console.warn('⚠️ SignalR connection error: ', err);
        this.connectionStateSignal.set('disconnected');
      });
  }

  public stopSignalRConnection(): void {
    if (this.hubConnection) {
      this.clearSubscriptions().finally(() => {
        this.hubConnection?.stop();
        this.connectionStateSignal.set('disconnected');
      });
    }
  }

  /**
   * Updates SignalR criteria group subscriptions based on the user's active search & filter state.
   * Atomically leaves stale groups and joins required groups.
   */
  public async updateCriteriaSubscriptions(criteria: Partial<JobSearchCriteriaDto>): Promise<void> {
    const desiredGroups = computeDesiredGroups(criteria);

    if (!this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
      this.currentSubscribedGroups = desiredGroups;
      return;
    }

    const toLeave = Array.from(this.currentSubscribedGroups).filter(g => !desiredGroups.has(g));
    const toJoin = Array.from(desiredGroups).filter(g => !this.currentSubscribedGroups.has(g));

    if (toLeave.length === 0 && toJoin.length === 0) {
      return;
    }

    try {
      await this.hubConnection.invoke('UpdateCriteriaSubscription', toLeave, toJoin);
      this.currentSubscribedGroups = desiredGroups;
    } catch (err) {
      console.warn('Could not update criteria subscriptions:', err);
    }
  }

  /**
   * Cleans up all group subscriptions on component teardown to guarantee zero leaks.
   */
  public async clearSubscriptions(): Promise<void> {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      if (this.currentSubscribedGroups.size > 0) {
        try {
          await this.hubConnection.invoke('UpdateCriteriaSubscription', Array.from(this.currentSubscribedGroups), []);
        } catch (err) {
          console.warn('Error clearing criteria subscriptions:', err);
        }
      }
    }
    this.currentSubscribedGroups.clear();
  }

  public searchJobs(criteria: Partial<JobSearchCriteriaDto>): Observable<PagedResult<JobSearchResultDto>> {
    return this.http.post<PagedResult<JobSearchResultDto>>('/api/jobs/search', criteria);
  }

  public syncNow(): Observable<{ success: boolean; newJobsCreated: number; message: string }> {
    return this.http.post<{ success: boolean; newJobsCreated: number; message: string }>('/api/jobs/sync-now', {});
  }

  public applyToJob(jobId: string, notes?: string): Observable<any> {
    return this.http.post(`/api/jobs/${jobId}/apply`, { notes });
  }

  public getJobQuestions(jobId: string): Observable<JobQuestionDto[]> {
    return this.http.get<JobQuestionDto[]>(`/api/jobs/${jobId}/questions`);
  }

  public submitJobApplication(jobId: string, formData: FormData): Observable<{ applicationId: string }> {
    return this.http.post<{ applicationId: string }>(`/api/jobs/${jobId}/apply`, formData);
  }

  public getJobDetail(jobId: string): Observable<any> {
    return this.http.get<any>(`/api/job-postings/${jobId}`);
  }
}
