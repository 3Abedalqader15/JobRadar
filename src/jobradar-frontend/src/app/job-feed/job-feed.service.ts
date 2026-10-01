import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { Observable } from 'rxjs';
import { JobSearchCriteriaDto, JobSearchResultDto, PagedResult } from './job.model';
import { NotificationService } from '../notifications/notification.service';

@Injectable({
  providedIn: 'root'
})
export class JobFeedService {
  private http = inject(HttpClient);
  private notificationService = inject(NotificationService);

  private hubConnection: signalR.HubConnection | undefined;
  private currentSubscribedGroups = new Set<string>();

  // Angular 17 Signal for real-time newly broadcasted matching jobs
  public readonly newJobSignal = signal<JobSearchResultDto | null>(null);
  public readonly connectionStateSignal = signal<'disconnected' | 'connecting' | 'connected'>('disconnected');

  public startSignalRConnection(): void {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      return;
    }

    this.connectionStateSignal.set('connecting');

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/jobs')
      .withAutomaticReconnect()
      .build();

    this.hubConnection.onreconnected(() => {
      this.connectionStateSignal.set('connected');
      // Resubscribe to current criteria groups
      this.resubscribeAllGroups();
    });

    this.hubConnection.onclose(() => {
      this.connectionStateSignal.set('disconnected');
    });

    // 1. Relevance-scoped event listener
    this.hubConnection.on('ReceiveRelevantJob', (job: JobSearchResultDto) => {
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

    // Backward compatibility if global broadcast was invoked
    this.hubConnection.on('ReceiveNewJob', (job: JobSearchResultDto) => {
      job.isNew = true;
      this.newJobSignal.set(job);
    });

    this.hubConnection
      .start()
      .then(() => {
        console.log('✅ SignalR connection established to /hubs/jobs');
        this.connectionStateSignal.set('connected');
        // Join default broad group
        this.joinGroup('grp:all');
      })
      .catch(err => {
        console.warn('⚠️ SignalR connection error: ', err);
        this.connectionStateSignal.set('disconnected');
      });
  }

  public stopSignalRConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
      this.currentSubscribedGroups.clear();
      this.connectionStateSignal.set('disconnected');
    }
  }

  /**
   * Updates SignalR criteria group subscriptions based on the user's active search & filter state
   */
  public updateCriteriaSubscriptions(criteria: Partial<JobSearchCriteriaDto>): void {
    if (!this.hubConnection || this.hubConnection.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    const desiredGroups = new Set<string>();
    desiredGroups.add('grp:all');

    if (criteria.isRemote) {
      desiredGroups.add('grp:remote');
    }

    if (criteria.location && criteria.location.trim()) {
      const locSlug = criteria.location.trim().toLowerCase().replace(/[^a-z0-9]/g, '');
      if (locSlug) {
        desiredGroups.add(`grp:loc:${locSlug}`);
      }
    }

    if (criteria.employmentTypes && criteria.employmentTypes.length > 0) {
      for (const emp of criteria.employmentTypes) {
        desiredGroups.add(`grp:emp:${emp}`);
      }
    }

    if (criteria.experienceLevels && criteria.experienceLevels.length > 0) {
      for (const exp of criteria.experienceLevels) {
        desiredGroups.add(`grp:exp:${exp}`);
      }
    }

    if (criteria.skills && criteria.skills.length > 0) {
      for (const skill of criteria.skills) {
        const skillSlug = skill.trim().toLowerCase().replace(/[^a-z0-9]/g, '');
        if (skillSlug) {
          desiredGroups.add(`grp:skill:${skillSlug}`);
        }
      }
    }

    // Leave groups that are no longer active
    for (const group of this.currentSubscribedGroups) {
      if (!desiredGroups.has(group)) {
        this.leaveGroup(group);
      }
    }

    // Join new groups
    for (const group of desiredGroups) {
      if (!this.currentSubscribedGroups.has(group)) {
        this.joinGroup(group);
      }
    }
  }

  private joinGroup(groupName: string): void {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      this.hubConnection.invoke('JoinCriteriaGroup', groupName)
        .then(() => this.currentSubscribedGroups.add(groupName))
        .catch(err => console.warn(`Could not join group ${groupName}`, err));
    }
  }

  private leaveGroup(groupName: string): void {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      this.hubConnection.invoke('LeaveCriteriaGroup', groupName)
        .then(() => this.currentSubscribedGroups.delete(groupName))
        .catch(err => console.warn(`Could not leave group ${groupName}`, err));
    }
  }

  private resubscribeAllGroups(): void {
    for (const group of this.currentSubscribedGroups) {
      this.joinGroup(group);
    }
  }

  public searchJobs(criteria: Partial<JobSearchCriteriaDto>): Observable<PagedResult<JobSearchResultDto>> {
    return this.http.post<PagedResult<JobSearchResultDto>>('/api/jobs/search', criteria);
  }

  public applyToJob(jobId: string, notes?: string): Observable<any> {
    return this.http.post(`/api/jobs/${jobId}/apply`, { notes });
  }
}
