import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { JobFeedComponent } from './job-feed/job-feed.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, JobFeedComponent],
  template: '<app-job-feed></app-job-feed>',
  styleUrl: './app.component.css'
})
export class AppComponent {
  title = 'jobradar-frontend';
}
