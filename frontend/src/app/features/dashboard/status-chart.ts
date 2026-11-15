import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { StatusSlice } from './dashboard.view';

const size = 168;
const thickness = 16;
const radius = (size - thickness) / 2;
const circumference = 2 * Math.PI * radius;
/** Gap between doughnut segments, in px along the ring. */
const gap = 3;

interface Segment {
  status: string;
  dashArray: string;
  dashOffset: number;
}

/**
 * "Tasks by status" card: a 168px doughnut (16px ring) with the total in the centre,
 * a legend with dot, label, count and percentage, and the overdue note. Drawn as inline SVG so the
 * colours come straight from the tokens in both themes.
 */
@Component({
  selector: 'app-status-chart',
  imports: [MatIconModule],
  template: `
    <div class="body">
      <svg
        class="doughnut"
        [attr.viewBox]="'0 0 ' + size + ' ' + size"
        role="img"
        [attr.aria-label]="ariaLabel()"
      >
        <circle class="track" [attr.cx]="size / 2" [attr.cy]="size / 2" [attr.r]="radius" />
        @for (segment of segments(); track segment.status) {
          <circle
            class="segment"
            [class]="'segment s-' + segment.status.toLowerCase()"
            [attr.cx]="size / 2"
            [attr.cy]="size / 2"
            [attr.r]="radius"
            [attr.stroke-dasharray]="segment.dashArray"
            [attr.stroke-dashoffset]="segment.dashOffset"
          />
        }
        <text class="total tabular" x="50%" y="47%" text-anchor="middle" dominant-baseline="middle">{{ total() }}</text>
        <text class="total-label" x="50%" y="63%" text-anchor="middle" dominant-baseline="middle">tasks</text>
      </svg>

      <div class="legend-wrap">
        <ul class="legend" aria-hidden="true">
          @for (slice of slices(); track slice.status) {
            <li class="legend-row">
              <span class="dot" [class]="'dot s-' + slice.status.toLowerCase()"></span>
              <span class="label">{{ slice.label }}</span>
              <span class="count tabular">{{ slice.count }}</span>
              <span class="percent tabular">{{ slice.percent }}%</span>
            </li>
          }
        </ul>
        @if (overdue() > 0) {
          <p class="overdue-note">
            <mat-icon>error</mat-icon>
            {{ overdue() }} of the open tasks {{ overdue() === 1 ? 'is' : 'are' }} overdue
          </p>
        }
      </div>
    </div>
  `,
  styleUrl: './status-chart.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusChart {
  readonly slices = input.required<StatusSlice[]>();
  readonly total = input.required<number>();
  readonly overdue = input.required<number>();

  protected readonly size = size;
  protected readonly radius = radius;

  /** Segments clockwise from 12 o'clock; empty statuses are skipped. */
  protected readonly segments = computed<Segment[]>(() => {
    const total = this.total();
    if (total === 0) {
      return [];
    }
    const visible = this.slices().filter((s) => s.count > 0);
    const gapLength = visible.length > 1 ? gap : 0;
    let start = 0;
    return visible.map((slice) => {
      const length = (slice.count / total) * circumference;
      const drawn = Math.max(length - gapLength, 0.001);
      const segment = {
        status: slice.status,
        dashArray: `${drawn} ${circumference - drawn}`,
        // Offsets run counter-clockwise; the SVG is rotated -90deg so 0 is 12 o'clock.
        dashOffset: -start,
      };
      start += length;
      return segment;
    });
  });

  protected readonly ariaLabel = computed(() => {
    const parts = this.slices().map((s) => `${s.label} ${s.count} (${s.percent}%)`);
    return `Tasks by status, ${this.total()} tasks: ${parts.join(', ')}.`;
  });
}
