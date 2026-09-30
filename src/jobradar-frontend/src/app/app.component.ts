import { Component, effect } from '@angular/core';
import { RouterOutlet, RouterLink } from '@angular/router';
import { AuthService } from './auth/auth.service';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, CommonModule],
  template: `
    <header class="navbar">
      <div class="logo">
        <a routerLink="/">JobRadar</a>
      </div>
      <div class="nav-links">
        <ng-container *ngIf="authService.currentUserSignal() as user; else loggedOut">
          <span class="user-email">{{ user.email }}</span>
          <button (click)="logout()" class="btn-logout">Logout</button>
        </ng-container>
        <ng-template #loggedOut>
          <a routerLink="/login" class="btn-login">Login / Sign Up</a>
        </ng-template>
      </div>
    </header>
    <main>
      <router-outlet></router-outlet>
    </main>
  `,
  styles: [`
    .navbar { display: flex; justify-content: space-between; align-items: center; padding: 15px 30px; background: #007bff; color: white; }
    .logo a { color: white; text-decoration: none; font-size: 1.5rem; font-weight: bold; }
    .nav-links { display: flex; gap: 15px; align-items: center; }
    .user-email { font-size: 0.9rem; }
    .btn-login, .btn-logout { background: white; color: #007bff; padding: 5px 15px; border: none; border-radius: 4px; text-decoration: none; cursor: pointer; font-weight: bold; }
    .btn-logout:hover, .btn-login:hover { background: #f0f0f0; }
    main { padding: 20px; }
  `]
})
export class AppComponent {
  constructor(public authService: AuthService) {}

  logout() {
    this.authService.logout().subscribe();
  }
}
