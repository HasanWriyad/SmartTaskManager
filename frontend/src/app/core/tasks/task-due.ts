import { TaskStatus } from './task.models';

/**
 * "Today" as the API defines it: the current UTC date, yyyy-mm-dd. The overdue
 * highlight, the relative due captions and the due-date filters all use it, so they agree with the
 * API's overdue filter and the dashboard (which returns its own asOf; use that where it is available).
 */
export function utcToday(now = new Date()): string {
  return now.toISOString().slice(0, 10);
}

/**
 * A calendar date the user picked (a local Date at midnight) to yyyy-mm-dd, unshifted. This is a date
 * value, not a moment in time, so it must not go through UTC conversion.
 */
export function toIsoDate(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** yyyy-mm-dd plus days, as yyyy-mm-dd (pure calendar arithmetic, in UTC so DST never shifts it). */
export function addDays(isoDate: string, days: number): string {
  const [y, m, d] = isoDate.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d + days)).toISOString().slice(0, 10);
}

/** Whole days from `from` to `to` (both yyyy-mm-dd). */
export function daysBetween(from: string, to: string): number {
  return Math.round((Date.parse(to) - Date.parse(from)) / 86_400_000);
}

/** Open (to do or in progress), due before today. */
export function isOverdue(dueDate: string | null, status: TaskStatus, today: string): boolean {
  return !!dueDate && dueDate < today && (status === 'ToDo' || status === 'InProgress');
}

/**
 * The caption under a due date: "Done", "Cancelled", "Overdue by 3 days", "Today",
 * "Tomorrow", "In 4 days", or "3 days ago" for a closed task.
 */
export function dueCaption(dueDate: string, status: TaskStatus, today: string): string {
  if (status === 'Completed') {
    return 'Done';
  }
  if (status === 'Cancelled') {
    return 'Cancelled';
  }
  const days = daysBetween(today, dueDate);
  if (days < 0) {
    return `Overdue by ${-days} ${days === -1 ? 'day' : 'days'}`;
  }
  if (days === 0) {
    return 'Today';
  }
  return days === 1 ? 'Tomorrow' : `In ${days} days`;
}
