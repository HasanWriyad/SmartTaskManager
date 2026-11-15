import { TaskPriority, TaskStatus } from '../../core/tasks/task.models';

/**
 * GET /api/tasks row (tasks across every project the caller can see). canManage: edit, assign,
 * delete; canUpdateStatus: change the status (both computed by the API for the caller).
 */
export interface TaskListItem {
  canManage: boolean;
  canUpdateStatus: boolean;
  id: string;
  projectId: string;
  projectName: string;
  title: string;
  status: TaskStatus;
  priority: TaskPriority;
  /** yyyy-mm-dd */
  dueDate: string | null;
  assigneeId: string | null;
  assigneeName: string | null;
  completedAt: string | null;
  createdAt: string;
}

/** GET /api/projects/{projectId}/tasks/{id}. canManage: edit, assign, delete; canUpdateStatus: change status. */
export interface TaskDetails {
  id: string;
  projectId: string;
  projectName: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  dueDate: string | null;
  assigneeId: string | null;
  assigneeName: string | null;
  createdById: string;
  createdByName: string;
  completedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
  canManage: boolean;
  canUpdateStatus: boolean;
}

export interface CreateTaskRequest {
  title: string;
  description: string | null;
  priority: TaskPriority;
  dueDate: string | null;
  assigneeId: string | null;
}

/** PUT replaces all four fields; a null dueDate clears it. */
export interface UpdateTaskRequest {
  title: string;
  description: string | null;
  priority: TaskPriority;
  dueDate: string | null;
}

/** Field limits from the backend's TaskItem entity. */
export const taskTitleMaxLength = 200;
export const taskDescriptionMaxLength = 4000;
/** The AI endpoint accepts descriptions up to this length. */
export const aiInputMaxLength = 2000;

export type TaskSortField = 'title' | 'status' | 'priority' | 'dueDate';
/** Due date filter: any, overdue (API overdue=true), today, next 7 days, later. */
export type DueFilter = '' | 'overdue' | 'today' | 'week' | 'later';

export const dueFilterLabels: Record<Exclude<DueFilter, ''>, string> = {
  overdue: 'Overdue',
  today: 'Due today',
  week: 'Next 7 days',
  later: 'Later',
};

export const taskSortLabels: Record<TaskSortField, string> = {
  title: 'Title',
  status: 'Status',
  priority: 'Priority',
  dueDate: 'Due date',
};

/** Task list state; it lives in the URL query string. */
export interface TaskListState {
  search: string;
  status: TaskStatus | '';
  priority: TaskPriority | '';
  assigneeId: string;
  due: DueFilter;
  projectId: string;
  sortBy: TaskSortField;
  sortDirection: 'asc' | 'desc';
  page: number;
  pageSize: number;
}

export const taskPageSizes = [5, 10, 25];

export const defaultTaskListState: TaskListState = {
  search: '',
  status: '',
  priority: '',
  assigneeId: '',
  due: '',
  projectId: '',
  sortBy: 'dueDate',
  sortDirection: 'asc',
  page: 1,
  pageSize: 10,
};
