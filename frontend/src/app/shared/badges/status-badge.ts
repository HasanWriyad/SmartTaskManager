import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TaskStatus, taskStatusLabels } from '../../core/tasks/task.models';

/**
 * Read-only task status badge: a 7px dot plus the label, so the meaning never relies on colour
 * alone. Cancelled is an outlined badge with a hollow dot.
 */
@Component({
  selector: 'app-status-badge',
  template: `<span class="dot" aria-hidden="true"></span>{{ label() }}`,
  styleUrl: './status-badge.scss',
  host: { '[class]': 'hostClass()' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBadge {
  readonly status = input.required<TaskStatus>();

  protected readonly label = computed(() => taskStatusLabels[this.status()]);
  protected readonly hostClass = computed(() => `status-${this.status().toLowerCase()}`);
}
