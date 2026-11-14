import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

/** Placeholder until the dashboard statistics are built. */
@Component({
  selector: 'app-dashboard',
  template: `
    <h1>Dashboard</h1>
    <p>Welcome, {{ user()?.fullName }}.</p>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Dashboard {
  protected readonly user = inject(AuthService).user;
}
