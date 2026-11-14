import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Admin-only (roleGuard). Placeholder until user management is built. */
@Component({
  selector: 'app-users',
  template: `
    <h1>Users</h1>
    <p>User management is coming next.</p>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Users {}
