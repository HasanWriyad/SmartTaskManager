import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { PriorityRow } from './dashboard.view';

/**
 * "Tasks by priority" card body: horizontal bars, Critical -> Low, 28px tall with a 6px
 * radius on a --surface-2 track, each with its count and percentage. Bars are scaled to the largest
 * priority. The "critical or high-priority still open" footnote is left out: the API's priority
 * counts include every status.
 */
@Component({
  selector: 'app-priority-chart',
  template: `
    <div class="bars" role="img" [attr.aria-label]="ariaLabel()">
      @for (row of rows(); track row.priority) {
        <div class="row" aria-hidden="true">
          <span class="label">{{ row.label }}</span>
          <span class="track">
            <span class="bar" [class]="'bar p-' + row.priority.toLowerCase()" [style.width.%]="row.barWidth"></span>
          </span>
          <span class="value tabular"><strong>{{ row.count }}</strong> · {{ row.percent }}%</span>
        </div>
      }
    </div>
  `,
  styles: `
    .bars {
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .row {
      display: grid;
      grid-template-columns: 72px 1fr 76px;
      align-items: center;
      gap: 16px;
    }
    .label {
      font-weight: 500;
    }
    .track {
      display: block;
      height: 28px;
      border-radius: var(--radius-badge);
      background: var(--surface-2);
      overflow: hidden;
    }
    .bar {
      display: block;
      height: 100%;
      border-radius: var(--radius-badge);
      transition: width 300ms ease;
    }
    .p-critical { background: var(--priority-critical-bar); }
    .p-high { background: var(--priority-high-bar); }
    .p-medium { background: var(--priority-medium-bar); }
    .p-low { background: var(--priority-low-bar); }
    .value {
      color: var(--text-muted);
      text-align: right;
      white-space: nowrap;
    }
    .value strong {
      color: var(--text);
      font-weight: 600;
    }
    @media (max-width: 479.98px) {
      .row {
        grid-template-columns: 60px 1fr 64px;
        gap: 10px;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PriorityChart {
  readonly rows = input.required<PriorityRow[]>();

  protected readonly ariaLabel = computed(
    () => `Tasks by priority: ${this.rows().map((r) => `${r.label} ${r.count} (${r.percent}%)`).join(', ')}.`,
  );
}
