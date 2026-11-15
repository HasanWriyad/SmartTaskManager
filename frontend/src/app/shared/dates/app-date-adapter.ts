import { Injectable, Provider } from '@angular/core';
import { DateAdapter, MAT_DATE_FORMATS, MatDateFormats, NativeDateAdapter } from '@angular/material/core';

const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
/** Marker for the date input's display format (the calendar's own labels use Intl formats). */
const inputFormat = 'd MMM y';

/**
 * Native dates shown in date fields like the lists show them ("16 Nov 2026", the DatePipe's
 * 'd MMM y'), instead of the browser locale's numeric format. Parsing accepts that format, a
 * day-first numeric date (16/11/2026, matching the display) and yyyy-mm-dd; anything else falls back
 * to the native parser.
 */
@Injectable()
export class AppDateAdapter extends NativeDateAdapter {
  override format(date: Date, displayFormat: object | string): string {
    if (displayFormat === inputFormat) {
      return `${date.getDate()} ${months[date.getMonth()]} ${date.getFullYear()}`;
    }
    return super.format(date, displayFormat as object);
  }

  override parse(value: unknown, parseFormat?: unknown): Date | null {
    if (typeof value === 'string') {
      const parsed = parseDate(value.trim());
      if (parsed !== undefined) {
        return parsed ?? this.invalid();
      }
    }
    return super.parse(value, parseFormat);
  }
}

/** A Date, null for an impossible date (e.g. 31 Feb), or undefined when the text is not one of our formats. */
function parseDate(text: string): Date | null | undefined {
  let match = /^(\d{1,2})\s+([a-z]{3,})\.?,?\s+(\d{4})$/i.exec(text);
  if (match) {
    const month = months.findIndex((m) => m.toLowerCase() === match![2].slice(0, 3).toLowerCase());
    return month < 0 ? null : build(+match[3], month, +match[1]);
  }
  match = /^(\d{1,2})[/.-](\d{1,2})[/.-](\d{4})$/.exec(text);
  if (match) {
    return build(+match[3], +match[2] - 1, +match[1]);
  }
  match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(text);
  if (match) {
    return build(+match[1], +match[2] - 1, +match[3]);
  }
  return undefined;
}

function build(year: number, month: number, day: number): Date | null {
  const date = new Date(year, month, day);
  return date.getFullYear() === year && date.getMonth() === month && date.getDate() === day ? date : null;
}

const appDateFormats: MatDateFormats = {
  parse: { dateInput: inputFormat },
  display: {
    dateInput: inputFormat,
    monthLabel: { month: 'short' },
    monthYearLabel: { year: 'numeric', month: 'short' },
    dateA11yLabel: { year: 'numeric', month: 'long', day: 'numeric' },
    monthYearA11yLabel: { year: 'numeric', month: 'long' },
  },
};

/** Use in place of provideNativeDateAdapter() on pages and dialogs with date fields. */
export function provideAppDateAdapter(): Provider[] {
  return [
    { provide: DateAdapter, useClass: AppDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: appDateFormats },
  ];
}
