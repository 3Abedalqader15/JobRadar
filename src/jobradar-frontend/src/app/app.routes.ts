import { Routes } from '@angular/router';
import { JobFeedComponent } from './job-feed/job-feed.component';
import { AuthComponent } from './auth/auth.component';
import { adminGuard } from './app.guards';

export const routes: Routes = [
  { path: '', component: JobFeedComponent },
  { path: 'login', component: AuthComponent },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () => import('./admin/admin.component').then(m => m.AdminComponent)
  },
  { path: '**', redirectTo: '' }
];
