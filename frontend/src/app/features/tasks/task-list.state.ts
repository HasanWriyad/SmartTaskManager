import { ParamMap, Params } from '@angular/router';
import { TaskPriority, TaskStatus } from '../../core/tasks/task.models';
import {
  DueFilter,
  TaskListState,
  TaskSortField,
  defaultTaskListState as defaults,
  taskPageSizes,
} from './task.models';

const statuses: TaskStatus[] = ['ToDo', 'InProgress', 'Completed', 'Cancelled'];
const priorities: TaskPriority[] = ['Low', 'Medium', 'High', 'Critical'];
const dueFilters: DueFilter[] = ['overdue', 'today', 'week', 'later'];
const sortFields: TaskSortField[] = ['title', 'status', 'priority', 'dueDate'];
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Reads the task list state from the URL; anything missing or invalid falls back to the default. */
export function parseTaskListState(query: ParamMap): TaskListState {
  const pick = <T extends string>(key: string, allowed: readonly T[], fallback: T): T => {
    const value = query.get(key) as T | null;
    return value && allowed.includes(value) ? value : fallback;
  };
  const id = (key: string) => {
    const value = query.get(key) ?? '';
    return guid.test(value) ? value.toLowerCase() : '';
  };
  const page = Number(query.get('page'));
  const pageSize = Number(query.get('pageSize'));

  return {
    search: (query.get('search') ?? '').trim().slice(0, 100),
    status: pick<TaskStatus | ''>('status', statuses, ''),
    priority: pick<TaskPriority | ''>('priority', priorities, ''),
    assigneeId: id('assigneeId'),
    due: pick<DueFilter>('due', dueFilters, ''),
    projectId: id('projectId'),
    sortBy: pick('sortBy', sortFields, defaults.sortBy),
    sortDirection: pick<'asc' | 'desc'>('sortDirection', ['asc', 'desc'], defaults.sortDirection),
    page: Number.isInteger(page) && page >= 1 ? page : defaults.page,
    pageSize: taskPageSizes.includes(pageSize) ? pageSize : defaults.pageSize,
  };
}

/** The URL query for a state: only values that differ from the default, so links stay short. */
export function taskListQueryParams(state: TaskListState): Params {
  const params: Params = {};
  for (const key of Object.keys(defaults) as (keyof TaskListState)[]) {
    params[key] = state[key] !== defaults[key] && state[key] !== '' ? state[key] : null;
  }
  return params;
}

export function hasTaskFilters(state: TaskListState): boolean {
  return !!(state.search || state.status || state.priority || state.assigneeId || state.due || state.projectId);
}
