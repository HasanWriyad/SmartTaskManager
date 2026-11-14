import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { MatIconRegistry } from '@angular/material/icon';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { errorInterceptor } from './core/api/error.interceptor';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    // Route params and query params become component inputs (e.g. Login's returnUrl).
    provideRouter(routes, withComponentInputBinding()),
    // Order matters: errorInterceptor is outermost, so it sees the final result after
    // authInterceptor's refresh-and-retry.
    provideHttpClient(withFetch(), withInterceptors([errorInterceptor, authInterceptor])),
    provideAppInitializer(() => {
      // index.html loads the Material Symbols font; make <mat-icon> use it.
      inject(MatIconRegistry).setDefaultFontSetClass('material-symbols-outlined');
      // Restore the session from the refresh cookie before the first navigation, so the guards
      // already know whether the user is signed in. Never fails: no cookie simply means signed out.
      return inject(AuthService).restoreSession();
    }),
  ],
};
