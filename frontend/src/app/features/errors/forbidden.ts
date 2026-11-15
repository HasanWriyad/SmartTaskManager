import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

/** Shown inside the shell (header title "Access denied") when roleGuard rejects a page. */
@Component({
  selector: 'app-forbidden',
  imports: [RouterLink, MatButtonModule, MatIconModule],
  template: `
    <section class="card" aria-labelledby="forbidden-title">
      <span class="icon-tile" aria-hidden="true"><mat-icon>lock</mat-icon></span>
      <h2 id="forbidden-title" class="title">You don't have access to this page</h2>
      <p class="text">Your role doesn't include this page. If you think it should, ask an admin.</p>
      <a mat-flat-button routerLink="/dashboard">Back to the dashboard</a>
    </section>
  `,
  styles: `
    .card {
      display: flex;
      flex-direction: column;
      align-items: center;
      max-width: 480px;
      margin: 40px auto 0;
      padding: 32px 24px;
      border: 1px solid var(--border);
      border-radius: var(--radius-card);
      background: var(--surface);
      text-align: center;
    }
    .icon-tile {
      display: grid;
      place-items: center;
      width: 48px;
      height: 48px;
      margin-bottom: 16px;
      border-radius: var(--radius-card);
      background: var(--overdue-bg);
      color: var(--overdue-text);
    }
    .icon-tile mat-icon {
      width: 24px;
      height: 24px;
      font-size: 24px;
      line-height: 24px;
    }
    .title {
      font-size: 18px;
      line-height: 26px;
      font-weight: 600;
    }
    .text {
      margin: 8px 0 24px;
      color: var(--text-muted);
    }
    a {
      min-width: 200px;
      height: 44px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Forbidden {}
