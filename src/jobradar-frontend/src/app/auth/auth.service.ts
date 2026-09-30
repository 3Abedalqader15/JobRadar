import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface AuthResponse {
  token: string;
  refreshToken: string;
  userId: string;
  email: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  public currentUserSignal = signal<{ userId: string; email: string } | null>(null);

  constructor(private http: HttpClient) {
    this.checkInitialAuth();
  }

  private checkInitialAuth() {
    const token = localStorage.getItem('jwt');
    const userId = localStorage.getItem('userId');
    const email = localStorage.getItem('email');
    if (token && userId && email) {
      this.currentUserSignal.set({ userId, email });
    }
  }

  login(credentials: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/login', credentials).pipe(
      tap(res => this.handleAuthResponse(res))
    );
  }

  register(userData: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/register', userData).pipe(
      tap(res => this.handleAuthResponse(res))
    );
  }

  logout(): Observable<any> {
    return this.http.post('/api/auth/logout', {}).pipe(
      tap(() => {
        localStorage.removeItem('jwt');
        localStorage.removeItem('refreshToken');
        localStorage.removeItem('userId');
        localStorage.removeItem('email');
        this.currentUserSignal.set(null);
      })
    );
  }

  refreshToken(): Observable<AuthResponse> {
    const refreshToken = localStorage.getItem('refreshToken');
    return this.http.post<AuthResponse>('/api/auth/refresh-token', { refreshToken }).pipe(
      tap(res => this.handleAuthResponse(res))
    );
  }

  private handleAuthResponse(res: AuthResponse) {
    localStorage.setItem('jwt', res.token);
    localStorage.setItem('refreshToken', res.refreshToken);
    localStorage.setItem('userId', res.userId);
    localStorage.setItem('email', res.email);
    this.currentUserSignal.set({ userId: res.userId, email: res.email });
  }

  getToken(): string | null {
    return localStorage.getItem('jwt');
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }
}
