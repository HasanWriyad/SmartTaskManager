import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The SmartTask mark: an accent tile holding a small checklist (a checked first row and two
 * open task lines). Colours come from the tokens, so it follows the light/dark theme.
 * The favicons in public/ are a simplified version (tile and a bold check only).
 */
@Component({
  selector: 'app-logo-mark',
  template: `
    <svg viewBox="0 0 48 48" [attr.width]="size()" [attr.height]="size()" aria-hidden="true" focusable="false">
      <rect width="48" height="48" rx="12" class="tile" />
      <g class="ink" fill="none" stroke-width="3.5" stroke-linecap="round" stroke-linejoin="round">
        <path d="M12.5 16.5l3.75 3.75 7.25-7.75" />
        <path d="M28.5 16.5H35.5" />
        <path class="open" d="M12.5 27H35.5" />
        <path class="open" d="M12.5 35H27.5" />
      </g>
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      flex: none;
    }
    .tile {
      fill: var(--accent);
    }
    .ink {
      stroke: var(--on-accent);
    }
    .open {
      opacity: 0.6;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogoMark {
  /** Rendered width and height in px. */
  readonly size = input(32);
}
