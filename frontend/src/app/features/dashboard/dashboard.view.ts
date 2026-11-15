import {
  TaskPriority,
  TaskStatus,
  taskPriorityLabels,
  taskStatusLabels,
} from '../../core/tasks/task.models';
import { Dashboard } from './dashboard.models';

/** Pure view-model helpers for the dashboard page (no Angular, no HTTP). */

export type CardTone = 'neutral' | 'completed' | 'pending' | 'overdue' | 'accent';

export interface SummaryCard {
  label: string;
  icon: string;
  tone: CardTone;
  value: number;
  footnote: string;
  /** Overdue shows its value in red (spec §7.2). */
  alert: boolean;
}

export interface StatusSlice {
  status: TaskStatus;
  label: string;
  count: number;
  percent: number;
}

export interface PriorityRow {
  priority: TaskPriority;
  label: string;
  count: number;
  percent: number;
  /** Bar length relative to the largest priority, 0-100. */
  barWidth: number;
}

/** Legend and doughnut order (spec screenshot): Completed, In progress, To do, then Cancelled. */
const statusOrder: TaskStatus[] = ['Completed', 'InProgress', 'ToDo', 'Cancelled'];
/** Bar chart order (spec §7.2): Critical -> High -> Medium -> Low. */
const priorityOrder: TaskPriority[] = ['Critical', 'High', 'Medium', 'Low'];

export function percentOf(count: number, total: number): number {
  return total > 0 ? Math.round((count / total) * 100) : 0;
}

export function summaryCards(d: Dashboard, isAdmin: boolean): SummaryCard[] {
  const byStatus = countsByStatus(d);
  return [
    {
      label: 'Total projects',
      icon: 'folder',
      tone: 'neutral',
      value: d.totalProjects,
      footnote: isAdmin ? 'Across all teams' : 'Projects you belong to',
      alert: false,
    },
    {
      label: 'Total tasks',
      icon: 'format_list_bulleted',
      tone: 'neutral',
      value: d.totalTasks,
      footnote: isAdmin ? 'In all projects' : 'In your projects',
      alert: false,
    },
    {
      label: 'Completed',
      icon: 'check_circle',
      tone: 'completed',
      value: d.completion.completed,
      footnote: `${percentOf(d.completion.completed, d.totalTasks)}% of all tasks`,
      alert: false,
    },
    {
      label: 'Pending',
      icon: 'schedule',
      tone: 'pending',
      value: d.completion.pending,
      footnote: `${byStatus.ToDo} to do · ${byStatus.InProgress} in progress`,
      alert: false,
    },
    {
      label: 'Overdue',
      icon: 'error',
      tone: 'overdue',
      value: d.overdueCount,
      footnote: 'Past due, not completed',
      alert: d.overdueCount > 0,
    },
    {
      // Label per CLAUDE.md: the API returns only the caller's open (pending) tasks.
      label: 'My open tasks',
      icon: 'person',
      tone: 'accent',
      value: d.myTasks.pending,
      footnote: d.myTasks.overdue > 0 ? `Assigned to you · ${d.myTasks.overdue} overdue` : 'Assigned to you',
      alert: false,
    },
  ];
}

export function statusSlices(d: Dashboard): StatusSlice[] {
  const byStatus = countsByStatus(d);
  return statusOrder.map((status) => ({
    status,
    label: taskStatusLabels[status],
    count: byStatus[status],
    percent: percentOf(byStatus[status], d.totalTasks),
  }));
}

export function priorityRows(d: Dashboard): PriorityRow[] {
  const counts = Object.fromEntries(priorityOrder.map((p) => [p, 0])) as Record<TaskPriority, number>;
  for (const entry of d.tasksByPriority) {
    counts[entry.priority] = entry.count;
  }
  const max = Math.max(0, ...Object.values(counts));
  return priorityOrder.map((priority) => ({
    priority,
    label: taskPriorityLabels[priority],
    count: counts[priority],
    percent: percentOf(counts[priority], d.totalTasks),
    barWidth: max > 0 ? (counts[priority] / max) * 100 : 0,
  }));
}

/** "Today", "Tomorrow", "In N days" relative to the API's asOf date (both yyyy-mm-dd, UTC). */
export function relativeDue(dueDate: string, asOf: string): string {
  const days = Math.round((Date.parse(dueDate) - Date.parse(asOf)) / 86_400_000);
  if (days === 0) {
    return 'Today';
  }
  if (days === 1) {
    return 'Tomorrow';
  }
  return days > 1 ? `In ${days} days` : `${-days} days ago`;
}

function countsByStatus(d: Dashboard): Record<TaskStatus, number> {
  const counts: Record<TaskStatus, number> = { ToDo: 0, InProgress: 0, Completed: 0, Cancelled: 0 };
  for (const entry of d.tasksByStatus) {
    counts[entry.status] = entry.count;
  }
  return counts;
}
