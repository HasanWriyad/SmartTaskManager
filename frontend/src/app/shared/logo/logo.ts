import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** The SmartTask mark: a 32px accent tile with a check, plus the wordmark (17px/700). */
@Component({
  selector: 'app-logo',
  imports: [MatIconModule],
  template: `
    <span class="tile" aria-hidden="true"><mat-icon>check</mat-icon></span>
    @if (wordmark()) {
      <span class="wordmark">SmartTask</span>
    }
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: 12px;
      color: var(--text);
    }
    .tile {
      display: grid;
      place-items: center;
      flex: none;
      width: 32px;
      height: 32px;
      border-radius: var(--radius-control);
      background: var(--accent);
      color: var(--on-accent);
    }
    .tile mat-icon {
      width: 18px;
      height: 18px;
      font-size: 18px;
      line-height: 18px;
      font-variation-settings: 'wght' 600;
    }
    .wordmark {
      font-size: 17px;
      line-height: 24px;
      font-weight: 700;
      letter-spacing: -0.01em;
      white-space: nowrap;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Logo {
  readonly wordmark = input(true);
}
