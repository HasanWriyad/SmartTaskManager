import { TaskStatus } from './task.models';

/** Today's local calendar date as yyyy-mm-dd. */
export function localToday(now = new Date()): string {
  return toIsoDate(now);
}

/** Local calendar date to yyyy-mm-dd (not shifted to UTC). */
export function toIsoDate(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** yyyy-mm-dd plus days, as yyyy-mm-dd. */
export function addDays(isoDate: string, days: number): string {
  const [y, m, d] = isoDate.split('-').map(Number);
  return toIsoDate(new Date(y, m - 1, d + days));
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
 * The caption under a due date (spec §7.3): "Done", "Cancelled", "Overdue by 3 days", "Today",
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
