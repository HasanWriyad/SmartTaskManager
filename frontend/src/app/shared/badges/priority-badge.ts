import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TaskPriority, taskPriorityLabels, taskPriorityLevels } from '../../core/tasks/task.models';

/**
 * Read-only task priority badge: a 15x12 icon of four signal bars (1 filled for Low ... 4 for
 * Critical; unfilled bars at 30% opacity) plus the label, so the meaning never relies on colour alone.
 */
@Component({
  selector: 'app-priority-badge',
  template: `
    <svg class="bars" width="15" height="12" viewBox="0 0 15 12" aria-hidden="true" focusable="false">
      @for (bar of bars; track bar) {
        <rect
          [attr.x]="bar * 4"
          [attr.y]="12 - (bar + 1) * 3"
          width="3"
          [attr.height]="(bar + 1) * 3"
          rx="0.75"
          [class.off]="bar >= level()"
        />
      }
    </svg>
    {{ label() }}
  `,
  styleUrl: './priority-badge.scss',
  host: { '[class]': 'hostClass()' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PriorityBadge {
  readonly priority = input.required<TaskPriority>();

  protected readonly bars = [0, 1, 2, 3];
  protected readonly label = computed(() => taskPriorityLabels[this.priority()]);
  protected readonly level = computed(() => taskPriorityLevels[this.priority()]);
  protected readonly hostClass = computed(() => `priority-${this.priority().toLowerCase()}`);
}
