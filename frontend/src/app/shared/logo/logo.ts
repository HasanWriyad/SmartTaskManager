import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { LogoMark } from './logo-mark';

/** The 32px SmartTask mark plus the wordmark (17px/700), used in the sidebar and the auth top bar. */
@Component({
  selector: 'app-logo',
  imports: [LogoMark],
  template: `
    <app-logo-mark [size]="32" />
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
