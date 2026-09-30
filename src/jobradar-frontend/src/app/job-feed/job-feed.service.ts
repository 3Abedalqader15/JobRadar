import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { Observable } from 'rxjs';
import { JobSearchResultDto, PagedResult } from './job.model';

@Injectable({
  providedIn: 'root'
})
export class JobFeedService {
  private hubConnection: signalR.HubConnection | undefined;
  
  // Angular 17 Signal for real-time newly broadcasted jobs
  public readonly newJobSignal = signal<JobSearchResultDto | null>(null);

  constructor(private http: HttpClient) { }

  public startSignalRConnection(): void {
    // Note: User needs to install '@microsoft/signalr' via npm
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/jobs') // Setup proxy in angular.json or use full backend URL
      .withAutomaticReconnect()
      .build();

    this.hubConnection
      .start()
      .then(() => console.log('✅ SignalR connection established'))
      .catch(err => console.error('❌ Error while starting SignalR connection: ', err));

    this.hubConnection.on('ReceiveNewJob', (job: JobSearchResultDto) => {
      // Mark it as new for the UI badge
      job.isNew = true;
      // Set the signal to trigger an effect in the component
      this.newJobSignal.set(job);
    });
  }

  public stopSignalRConnection(): void {
    this.hubConnection?.stop();
  }

  public searchJobs(filters: any): Observable<PagedResult<JobSearchResultDto>> {
    // Make sure API base URL is configured via environment or proxy
    return this.http.post<PagedResult<JobSearchResultDto>>('/api/jobs/search', filters);
  }

  public applyToJob(jobId: string, notes?: string): Observable<any> {
    return this.http.post(`/api/jobs/${jobId}/apply`, { notes });
  }
}
