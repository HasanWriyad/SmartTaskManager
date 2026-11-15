import { HttpClient, httpResource } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiResponse, PagedResult } from '../../core/api/api.models';
import { silentErrors } from '../../core/api/http-context';
import { addDays, localToday } from '../../core/tasks/task-due';
import { TaskStatus } from '../../core/tasks/task.models';
import { CreateTaskRequest, TaskDetails, TaskListItem, TaskListState, UpdateTaskRequest } from './task.models';

const tasksUrl = (projectId: string) => `/api/projects/${encodeURIComponent(projectId)}/tasks`;

/**
 * Tasks API. The list is a resource; reads of one task and every change are observables. Every request
 * skips the global error snackbar: the page and the dialog show failures in context.
 */
@Injectable({ providedIn: 'root' })
export class TaskService {
  private readonly http = inject(HttpClient);

  /** GET /api/tasks for the given state. Call from an injection context. */
  list(state: () => TaskListState) {
    return httpResource<PagedResult<TaskListItem>>(
      () => ({ url: '/api/tasks', params: toQueryParams(state()), context: silentErrors() }),
      { parse: (body) => (body as ApiResponse<PagedResult<TaskListItem>>).data! },
    );
  }

  /**
   * GET /api/projects/{projectId}/tasks in the API's default order (highest priority, then earliest due
   * date). No request while projectId is undefined. Call from an injection context.
   */
  listByProject(query: () => { projectId: string; page: number; pageSize: number } | undefined) {
    return httpResource<PagedResult<TaskListItem>>(
      () => {
        const q = query();
        return q
          ? { url: tasksUrl(q.projectId), params: { page: q.page, pageSize: q.pageSize }, context: silentErrors() }
          : undefined;
      },
      { parse: (body) => (body as ApiResponse<PagedResult<TaskListItem>>).data! },
    );
  }

  get(projectId: string, taskId: string): Observable<TaskDetails> {
    return this.http
      .get<ApiResponse<TaskDetails>>(`${tasksUrl(projectId)}/${encodeURIComponent(taskId)}`, { context: silentErrors() })
      .pipe(map((response) => response.data!));
  }

  create(projectId: string, request: CreateTaskRequest): Observable<TaskDetails> {
    return this.http
      .post<ApiResponse<TaskDetails>>(tasksUrl(projectId), request, { context: silentErrors() })
      .pipe(map((response) => response.data!));
  }

  update(projectId: string, taskId: string, request: UpdateTaskRequest): Observable<TaskDetails> {
    return this.http
      .put<ApiResponse<TaskDetails>>(`${tasksUrl(projectId)}/${taskId}`, request, { context: silentErrors() })
      .pipe(map((response) => response.data!));
  }

  /** Assigns to a project member, or unassigns (a separate DELETE) when assigneeId is null. */
  assign(projectId: string, taskId: string, assigneeId: string | null): Observable<TaskDetails> {
    const url = `${tasksUrl(projectId)}/${taskId}/assignee`;
    const request$ = assigneeId
      ? this.http.put<ApiResponse<TaskDetails>>(url, { assigneeId }, { context: silentErrors() })
      : this.http.delete<ApiResponse<TaskDetails>>(url, { context: silentErrors() });
    return request$.pipe(map((response) => response.data!));
  }

  changeStatus(projectId: string, taskId: string, status: TaskStatus): Observable<TaskDetails> {
    return this.http
      .put<ApiResponse<TaskDetails>>(`${tasksUrl(projectId)}/${taskId}/status`, { status }, { context: silentErrors() })
      .pipe(map((response) => response.data!));
  }

  delete(projectId: string, taskId: string): Observable<void> {
    return this.http
      .delete<unknown>(`${tasksUrl(projectId)}/${taskId}`, { context: silentErrors() })
      .pipe(map(() => undefined));
  }
}

/**
 * State to API query. Due filters use the user's local "today"; "overdue" is the API's own filter
 * (due before today in UTC, to do or in progress).
 */
function toQueryParams(state: TaskListState): Record<string, string | number | boolean> {
  const params: Record<string, string | number | boolean> = {
    sortBy: state.sortBy,
    sortDirection: state.sortDirection === 'desc' ? 'Desc' : 'Asc',
    page: state.page,
    pageSize: state.pageSize,
  };
  const optional: Record<string, string> = {
    search: state.search,
    status: state.status,
    priority: state.priority,
    assigneeId: state.assigneeId,
    projectId: state.projectId,
  };
  for (const [key, value] of Object.entries(optional)) {
    if (value) {
      params[key] = value;
    }
  }

  const today = localToday();
  switch (state.due) {
    case 'overdue':
      params['overdue'] = true;
      break;
    case 'today':
      params['dueFrom'] = today;
      params['dueTo'] = today;
      break;
    case 'week':
      params['dueFrom'] = today;
      params['dueTo'] = addDays(today, 7);
      break;
    case 'later':
      params['dueFrom'] = addDays(today, 8);
      break;
  }
  return params;
}
