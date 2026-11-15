import { DOCUMENT, Injectable, computed, effect, inject, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

/** localStorage key for the user's explicit choice; index.html reads the same key before first paint. */
const storageKey = 'stm-theme';

/**
 * Light/dark theme. Follows prefers-color-scheme until the user flips the header toggle; that choice
 * is saved and wins from then on. The theme is the `dark-theme` class on <html>, which switches the
 * design tokens (styles/_tokens.scss) and Material's colour scheme.
 *
 * Only this UI preference is kept in localStorage; tokens never are.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly media = this.document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)') ?? null;

  private readonly systemTheme = signal<Theme>(this.media?.matches ? 'dark' : 'light');
  private readonly savedTheme = signal<Theme | null>(readSaved());

  readonly theme = computed<Theme>(() => this.savedTheme() ?? this.systemTheme());
  readonly isDark = computed(() => this.theme() === 'dark');

  constructor() {
    this.media?.addEventListener('change', (event) => this.systemTheme.set(event.matches ? 'dark' : 'light'));
    effect(() => this.document.documentElement.classList.toggle('dark-theme', this.isDark()));
  }

  toggle(): void {
    const next: Theme = this.isDark() ? 'light' : 'dark';
    this.savedTheme.set(next);
    try {
      localStorage.setItem(storageKey, next);
    } catch {
      // Storage blocked (private mode, policy): the choice lasts for this page only.
    }
  }
}

function readSaved(): Theme | null {
  try {
    const value = localStorage.getItem(storageKey);
    return value === 'light' || value === 'dark' ? value : null;
  } catch {
    return null;
  }
}
