import { HttpInterceptorFn, HttpErrorResponse, HttpRequest, HttpHandlerFn, HttpEvent } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService, AuthResponse } from './auth.service';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError, BehaviorSubject, filter, take, Observable } from 'rxjs';

let isRefreshing = false;
let refreshTokenSubject: BehaviorSubject<string | null> = new BehaviorSubject<string | null>(null);

export const authInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const token = authService.getToken();
  const authReq = req.clone({
    withCredentials: true,
    headers: token ? req.headers.set('Authorization', `Bearer ${token}`) : req.headers
  });

  return next(authReq).pipe(
    catchError((error) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !req.url.includes('/api/auth/')) {
        return handle401Error(authReq, next, authService, router);
      }
      return throwError(() => error);
    })
  );
};

function handle401Error(request: HttpRequest<unknown>, next: HttpHandlerFn, authService: AuthService, router: Router): Observable<HttpEvent<unknown>> {
  if (!isRefreshing) {
    isRefreshing = true;
    refreshTokenSubject.next(null);

    return authService.refreshToken().pipe(
      switchMap((res: AuthResponse) => {
        isRefreshing = false;
        const newAccessToken = res.accessToken ?? authService.getToken();
        refreshTokenSubject.next(newAccessToken);
        return next(request.clone({
          withCredentials: true,
          headers: request.headers.set('Authorization', `Bearer ${newAccessToken}`)
        }));
      }),
      catchError((err) => {
        isRefreshing = false;
        authService.logout().subscribe({
          next: () => router.navigate(['/login']),
          error: () => router.navigate(['/login'])
        });
        return throwError(() => err);
      })
    );
  } else {
    return refreshTokenSubject.pipe(
      filter(token => token != null),
      take(1),
      switchMap(jwt => {
        return next(request.clone({
          withCredentials: true,
          headers: request.headers.set('Authorization', `Bearer ${jwt}`)
        }));
      })
    );
  }
}
