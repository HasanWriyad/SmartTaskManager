import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-forbidden',
  imports: [RouterLink, MatButtonModule, MatIconModule],
  template: `
    <section class="forbidden">
      <mat-icon class="icon">block</mat-icon>
      <h1>Access denied</h1>
      <p>Your role does not have access to this page.</p>
      <a mat-flat-button routerLink="/dashboard">Back to the dashboard</a>
    </section>
  `,
  styles: `
    .forbidden { display: grid; justify-items: center; gap: 8px; padding: 48px 16px; text-align: center; }
    .icon { width: 48px; height: 48px; font-size: 48px; color: var(--mat-sys-error); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Forbidden {}
