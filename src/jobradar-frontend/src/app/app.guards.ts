import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth/auth.service';

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAdminOrHR()) {
    return true;
  }

  if (!auth.isLoggedIn()) {
    router.navigate(['/login']);
  } else {
    router.navigate(['/']);
  }
  return false;
};

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isLoggedIn()) {
    return true;
  }

  router.navigate(['/login']);
  return false;
};
