import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const isApiRequest = (url: string) => url.startsWith('/api/');
// Login, register, refresh and logout run on the cookie; a 401 there is a real answer, not an expired token.
const isAuthEndpoint = (url: string) => url.startsWith('/api/auth/');

const withAccessToken = (request: HttpRequest<unknown>, token: string | null) =>
  token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

/**
 * Adds the access token to API calls. On a 401 it refreshes the session once and retries the request
 * with the new token. The retry goes straight to the next handler, so it cannot trigger another
 * refresh. If the refresh fails, the session is cleared and the user is sent to sign in again.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isApiRequest(request.url) || isAuthEndpoint(request.url)) {
    return next(request);
  }

  const auth = inject(AuthService);
  const router = inject(Router);

  return next(withAccessToken(request, auth.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      // The token expired, or the user's role changed and the API rejected the old token.
      return auth.refresh().pipe(
        switchMap((refreshed) => {
          if (!refreshed) {
            auth.expireSession(router.url);
            return throwError(() => error);
          }
          return next(withAccessToken(request, auth.accessToken()));
        }),
      );
    }),
  );
};
