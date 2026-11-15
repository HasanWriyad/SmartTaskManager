import { Injectable, Injector, inject } from '@angular/core';
import type { MatSnackBar, MatSnackBarConfig } from '@angular/material/snack-bar';

/**
 * Snackbar messages. The snackbar (and the CDK overlay behind it) is loaded on first use, so it is
 * not part of the initial bundle; the HTTP error interceptor uses this service at startup.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly injector = inject(Injector);
  private snackBar: Promise<MatSnackBar> | null = null;

  success(message: string): void {
    this.open(message, undefined, { duration: 3000 });
  }

  error(message: string): void {
    this.open(message, 'Dismiss', { duration: 6000, panelClass: 'snackbar-error' });
  }

  private open(message: string, action: string | undefined, config: MatSnackBarConfig): void {
    this.snackBar ??= import('@angular/material/snack-bar').then((m) => this.injector.get(m.MatSnackBar));
    void this.snackBar.then((snackBar) => snackBar.open(message, action, config));
  }
}
