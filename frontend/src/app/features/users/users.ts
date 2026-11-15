import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Admin-only (roleGuard). Placeholder until user management is built. */
@Component({
  selector: 'app-users',
  template: `<p class="placeholder">User management is coming next.</p>`,
  styles: `.placeholder { margin: 0; color: var(--text-muted); }`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Users {}
