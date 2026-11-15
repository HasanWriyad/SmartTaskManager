import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Placeholder until the project list is built. */
@Component({
  selector: 'app-projects',
  template: `<p class="placeholder">The project list is coming next.</p>`,
  styles: `.placeholder { margin: 0; color: var(--text-muted); }`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Projects {}
