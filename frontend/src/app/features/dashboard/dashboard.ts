import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

/** Placeholder until the dashboard statistics are built. */
@Component({
  selector: 'app-dashboard',
  template: `<p class="placeholder">Welcome, {{ user()?.fullName }}. The dashboard statistics are coming next.</p>`,
  styles: `.placeholder { margin: 0; color: var(--text-muted); }`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Dashboard {
  protected readonly user = inject(AuthService).user;
}
