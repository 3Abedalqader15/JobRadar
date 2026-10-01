import { Injectable, signal } from '@angular/core';

export interface ToastNotification {
  id: string;
  type: 'info' | 'success' | 'warning' | 'job';
  title: string;
  message: string;
  timestamp: Date;
  jobId?: string;
  actionUrl?: string;
  actionLabel?: string;
  metaBadge?: string;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  public readonly toasts = signal<ToastNotification[]>([]);

  public show(params: {
    type?: 'info' | 'success' | 'warning' | 'job';
    title: string;
    message: string;
    jobId?: string;
    actionUrl?: string;
    actionLabel?: string;
    metaBadge?: string;
    durationMs?: number;
  }): string {
    const id = 'toast_' + Math.random().toString(36).substring(2, 9);
    const notification: ToastNotification = {
      id,
      type: params.type || 'info',
      title: params.title,
      message: params.message,
      timestamp: new Date(),
      jobId: params.jobId,
      actionUrl: params.actionUrl,
      actionLabel: params.actionLabel,
      metaBadge: params.metaBadge
    };

    // Prepend new toasts (latest on top, cap at 5)
    this.toasts.update(current => [notification, ...current.slice(0, 4)]);

    const duration = params.durationMs ?? (params.type === 'job' ? 8000 : 5000);
    setTimeout(() => {
      this.remove(id);
    }, duration);

    return id;
  }

  public remove(id: string): void {
    this.toasts.update(current => current.filter(t => t.id !== id));
  }

  public clearAll(): void {
    this.toasts.set([]);
  }
}
