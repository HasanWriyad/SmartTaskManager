import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Placeholder until the task list (GET /api/tasks) is built. */
@Component({
  selector: 'app-tasks',
  template: `<p class="placeholder">The task list is coming next.</p>`,
  styles: `.placeholder { margin: 0; color: var(--text-muted); }`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Tasks {}
