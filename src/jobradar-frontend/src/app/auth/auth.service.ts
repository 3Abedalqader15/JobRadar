import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { Router } from '@angular/router';

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  userId: string;
  email: string;
  fullName: string;
  roles: string[];
}

export interface CurrentUser {
  userId: string;
  email: string;
  fullName: string;
  roles: string[];
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  public currentUserSignal = signal<CurrentUser | null>(null);

  // Computed helpers
  public isAdmin = computed(() =>
    this.currentUserSignal()?.roles?.includes('Admin') ?? false
  );
  public isHR = computed(() =>
    this.currentUserSignal()?.roles?.includes('HR') ?? false
  );
  public isAdminOrHR = computed(() => this.isAdmin() || this.isHR());

  constructor(private http: HttpClient, private router: Router) {
    this.restoreSession();
  }

  private restoreSession() {
    const token = localStorage.getItem('jwt');
    const userId = localStorage.getItem('userId');
    const email = localStorage.getItem('email');
    const fullName = localStorage.getItem('fullName') ?? '';
    const roles = JSON.parse(localStorage.getItem('roles') ?? '[]');
    if (token && userId && email) {
      this.currentUserSignal.set({ userId, email, fullName, roles });
    }
  }

  login(credentials: { email: string; password: string }): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/login', credentials).pipe(
      tap(res => this.handleAuthResponse(res))
    );
  }

  register(userData: { email: string; password: string; fullName: string; role?: string }): Observable<any> {
    return this.http.post<any>('/api/auth/register', {
      email: userData.email,
      password: userData.password,
      fullName: userData.fullName,
      role: userData.role ?? 'User'
    });
  }

  logout(): Observable<any> {
    return this.http.post('/api/auth/logout', {}).pipe(
      tap(() => this.clearSession())
    );
  }

  logoutLocal(): void {
    this.clearSession();
    this.router.navigate(['/login']);
  }

  refreshToken(): Observable<AuthResponse> {
    const refreshToken = localStorage.getItem('refreshToken');
    return this.http.post<AuthResponse>('/api/auth/refresh-token', { refreshToken }).pipe(
      tap(res => this.handleAuthResponse(res))
    );
  }

  private handleAuthResponse(res: AuthResponse) {
    localStorage.setItem('jwt', res.accessToken);
    localStorage.setItem('refreshToken', res.refreshToken);
    localStorage.setItem('userId', res.userId);
    localStorage.setItem('email', res.email);
    localStorage.setItem('fullName', res.fullName ?? '');
    localStorage.setItem('roles', JSON.stringify(res.roles ?? []));
    this.currentUserSignal.set({
      userId: res.userId,
      email: res.email,
      fullName: res.fullName,
      roles: res.roles ?? []
    });
  }

  private clearSession() {
    ['jwt', 'refreshToken', 'userId', 'email', 'fullName', 'roles'].forEach(k =>
      localStorage.removeItem(k)
    );
    this.currentUserSignal.set(null);
  }

  getToken(): string | null {
    return localStorage.getItem('jwt');
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }
}
