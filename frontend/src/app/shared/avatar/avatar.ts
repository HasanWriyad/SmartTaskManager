import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Round avatar with initials. The current user's avatar uses the accent-soft colours (spec §1). */
@Component({
  selector: 'app-avatar',
  template: `{{ initials() }}`,
  styles: `
    :host {
      display: inline-grid;
      place-items: center;
      flex: none;
      border-radius: 50%;
      background: var(--surface-2);
      color: var(--text-muted);
      font-weight: 600;
      letter-spacing: 0.02em;
      user-select: none;
    }
    :host(.self) {
      background: var(--accent-soft);
      color: var(--accent-text);
    }
  `,
  host: {
    'aria-hidden': 'true',
    '[class.self]': 'self()',
    '[style.width.px]': 'size()',
    '[style.height.px]': 'size()',
    '[style.font-size.px]': 'size() >= 36 ? 13 : 11',
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Avatar {
  readonly name = input.required<string>();
  readonly size = input(36);
  readonly self = input(false);

  /** First letters of the first and last words ("Ayesha Rahman" -> "AR"). */
  protected readonly initials = computed(() => {
    const words = this.name().trim().split(/\s+/).filter(Boolean);
    if (words.length === 0) {
      return '?';
    }
    const first = words[0][0];
    const last = words.length > 1 ? words[words.length - 1][0] : '';
    return (first + last).toUpperCase();
  });
}
