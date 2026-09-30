import { Routes } from '@angular/router';
import { JobFeedComponent } from './job-feed/job-feed.component';
import { AuthComponent } from './auth/auth.component';

export const routes: Routes = [
  { path: '', component: JobFeedComponent },
  { path: 'login', component: AuthComponent },
  { path: '**', redirectTo: '' }
];
