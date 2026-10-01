import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NotificationService, ToastNotification } from './notification.service';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './toast-container.component.html',
  styleUrls: ['./toast-container.component.css']
})
export class ToastContainerComponent {
  public notificationService = inject(NotificationService);
  private router = inject(Router);

  public dismiss(toast: ToastNotification, event?: Event): void {
    if (event) {
      event.stopPropagation();
    }
    this.notificationService.remove(toast.id);
  }

  public onToastClick(toast: ToastNotification): void {
    if (toast.actionUrl) {
      window.open(toast.actionUrl, '_blank');
    } else if (toast.jobId) {
      // Could scroll or select job
    }
    this.dismiss(toast);
  }
}
