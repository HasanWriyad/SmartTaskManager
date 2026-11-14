import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, firstValueFrom, map, of, shareReplay, switchMap } from 'rxjs';
import { ApiResponse } from '../api/api.models';
import { silentErrors } from '../api/http-context';
import { AuthResponse, CurrentUser, LoginRequest, RegisterRequest, Role } from './auth.models';

const authUrl = '/api/auth';

/**
 * Session state in signals. The access token lives only in memory; the refresh token is an httpOnly
 * cookie the browser sends to /api/auth (same origin through the dev proxy).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly accessTokenState = signal<string | null>(null);
  private readonly userState = signal<CurrentUser | null>(null);

  /** The single refresh request currently in flight, shared by every caller (see refresh()). */
  private refreshInFlight$: Observable<boolean> | null = null;

  readonly user = this.userState.asReadonly();
  readonly isAuthenticated = computed(() => this.accessTokenState() !== null);
  readonly role = computed(() => this.userState()?.role ?? null);

  accessToken(): string | null {
    return this.accessTokenState();
  }

  hasAnyRole(roles: readonly Role[]): boolean {
    const role = this.role();
    return role !== null && roles.includes(role);
  }

  login(request: LoginRequest): Observable<CurrentUser> {
    return this.http
      .post<ApiResponse<AuthResponse>>(`${authUrl}/login`, request)
      .pipe(map((response) => this.startSession(response.data!)));
  }

  /** Registration creates a TeamMember and returns no tokens, so sign in right after. */
  register(request: RegisterRequest): Observable<CurrentUser> {
    return this.http
      .post<ApiResponse<unknown>>(`${authUrl}/register`, request)
      .pipe(switchMap(() => this.login({ email: request.email, password: request.password })));
  }

  /**
   * Exchanges the refresh cookie for a new access token. Emits true on success, false otherwise
   * (never errors). Only one request runs at a time: while one is in flight every caller, e.g. the
   * HTTP interceptor and session restore, shares it. Parallel refreshes would present the same
   * rotated token twice, which the API treats as token reuse and answers by ending the session.
   */
  refresh(): Observable<boolean> {
    if (this.refreshInFlight$) {
      return this.refreshInFlight$;
    }

    this.refreshInFlight$ = this.http
      .post<ApiResponse<AuthResponse>>(`${authUrl}/refresh`, null, { context: silentErrors() })
      .pipe(
        map((response) => {
          this.startSession(response.data!);
          return true;
        }),
        catchError(() => {
          this.clearSession();
          return of(false);
        }),
        finalize(() => (this.refreshInFlight$ = null)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    return this.refreshInFlight$;
  }

  /** Called once at startup: a valid refresh cookie signs the user back in after a reload. */
  restoreSession(): Promise<boolean> {
    return firstValueFrom(this.refresh());
  }

  /** Revokes the session on the server (best effort) and clears it locally. */
  logout(): Observable<void> {
    return this.http.post<unknown>(`${authUrl}/logout`, null, { context: silentErrors() }).pipe(
      catchError(() => of(null)),
      map(() => undefined),
      finalize(() => {
        this.clearSession();
        void this.router.navigate(['/login']);
      }),
    );
  }

  /** The refresh failed while the user was working: clear the session and ask them to sign in again. */
  expireSession(returnUrl: string): void {
    this.clearSession();
    void this.router.navigate(['/login'], { queryParams: { returnUrl, expired: true } });
  }

  private startSession(auth: AuthResponse): CurrentUser {
    this.accessTokenState.set(auth.accessToken);
    this.userState.set(auth.user);
    return auth.user;
  }

  private clearSession(): void {
    this.accessTokenState.set(null);
    this.userState.set(null);
  }
}
