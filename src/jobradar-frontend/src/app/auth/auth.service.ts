import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, catchError, of, shareReplay } from 'rxjs';
import { Router } from '@angular/router';

export interface AuthResponse {
  accessToken: string;
  refreshToken?: string;
  userId: string;
  email: string;
  fullName: string;
  roles: string[];
  companyId?: string | null;
  companyName?: string | null;
}

export interface CurrentUser {
  userId: string;
  email: string;
  fullName: string;
  roles: string[];
  companyId?: string | null;
  companyName?: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  // In-memory access token signal — NEVER written to localStorage or sessionStorage
  public accessTokenSignal = signal<string | null>(null);
  public currentUserSignal = signal<CurrentUser | null>(null);

  // Computed helpers
  public isAdmin = computed(() =>
    this.currentUserSignal()?.roles?.includes('Admin') ?? false
  );
  public isHR = computed(() =>
    this.currentUserSignal()?.roles?.includes('HR') ?? false
  );
  public isAdminOrHR = computed(() => this.isAdmin() || this.isHR());
  public companyId = computed(() => this.currentUserSignal()?.companyId ?? null);
  public companyName = computed(() => this.currentUserSignal()?.companyName ?? null);

  private restoreSession$?: Observable<AuthResponse | null>;

  constructor(private http: HttpClient, private router: Router) {
    this.restoreSession().subscribe();
  }

  public restoreSession(): Observable<AuthResponse | null> {
    if (!this.restoreSession$) {
      this.restoreSession$ = this.refreshToken().pipe(
        catchError(() => {
          this.clearSession();
          return of(null);
        }),
        shareReplay(1)
      );
    }
    return this.restoreSession$;
  }

  login(credentials: { email: string; password: string }): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/login', credentials, { withCredentials: true }).pipe(
      tap(res => this.handleAuthResponse(res))
    );
  }

  register(userData: { email: string; password: string; fullName: string; role?: string }): Observable<any> {
    return this.http.post<any>('/api/auth/register', {
      email: userData.email,
      password: userData.password,
      fullName: userData.fullName,
      role: userData.role ?? 'User'
    }, { withCredentials: true });
  }

  logout(): Observable<any> {
    return this.http.post('/api/auth/logout', {}, { withCredentials: true }).pipe(
      tap(() => this.clearSession()),
      catchError(() => {
        this.clearSession();
        return of(null);
      })
    );
  }

  logoutLocal(): void {
    this.clearSession();
    this.router.navigate(['/login']);
  }

  refreshToken(): Observable<AuthResponse> {
    const body = this.accessTokenSignal() ? { accessToken: this.accessTokenSignal() } : {};
    return this.http.post<AuthResponse>('/api/auth/refresh', body, { withCredentials: true }).pipe(
      tap(res => this.handleAuthResponse(res))
    );
  }

  private handleAuthResponse(res: AuthResponse) {
    this.accessTokenSignal.set(res.accessToken);
    this.currentUserSignal.set({
      userId: res.userId,
      email: res.email,
      fullName: res.fullName,
      roles: res.roles ?? [],
      companyId: res.companyId ?? null,
      companyName: res.companyName ?? null
    });
  }

  private clearSession() {
    this.restoreSession$ = undefined;
    this.accessTokenSignal.set(null);
    this.currentUserSignal.set(null);
  }

  getToken(): string | null {
    return this.accessTokenSignal();
  }

  isLoggedIn(): boolean {
    return !!this.accessTokenSignal();
  }
}
