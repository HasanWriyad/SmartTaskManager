import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Role } from './auth.models';
import { AuthService } from './auth.service';

// The session is restored in an app initializer before the first navigation, so these guards
// can read the signals synchronously.

/** Signed-in users only; others go to /login and come back afterwards. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.isAuthenticated()) {
    return true;
  }
  const returnUrl = state.url === '/' ? undefined : state.url;
  return inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl } });
};

/** For the sign-in and registration pages: signed-in users go straight into the app. */
export const guestGuard: CanActivateFn = () =>
  inject(AuthService).isAuthenticated() ? inject(Router).createUrlTree(['/dashboard']) : true;

/** Allows the route when the user has one of the roles in route data `roles`; otherwise shows /forbidden. */
export const roleGuard: CanActivateFn = (route) => {
  const roles = (route.data['roles'] as Role[] | undefined) ?? [];
  if (roles.length === 0 || inject(AuthService).hasAnyRole(roles)) {
    return true;
  }
  return inject(Router).createUrlTree(['/forbidden']);
};
