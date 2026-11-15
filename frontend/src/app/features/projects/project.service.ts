import { HttpClient, httpResource } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiResponse, PagedResult } from '../../core/api/api.models';
import { silentErrors } from '../../core/api/http-context';
import { ProjectDetails, ProjectListItem, ProjectListState, ProjectRequest } from './project.models';

const baseUrl = '/api/projects';

/**
 * Projects API. Loads are resources (value, isLoading, error, reload); changes are observables.
 * Every request skips the global error snackbar: pages and dialogs show failures in context.
 */
@Injectable({ providedIn: 'root' })
export class ProjectService {
  private readonly http = inject(HttpClient);

  /** The project list for the given state. Call from an injection context. */
  list(state: () => ProjectListState) {
    return httpResource<PagedResult<ProjectListItem>>(
      () => ({ url: baseUrl, params: toQueryParams(state()), context: silentErrors() }),
      { parse: (body) => (body as ApiResponse<PagedResult<ProjectListItem>>).data! },
    );
  }

  /** One project with its members; no request while id is undefined. */
  details(id: () => string | undefined) {
    return httpResource<ProjectDetails>(
      () => {
        const value = id();
        return value ? { url: `${baseUrl}/${encodeURIComponent(value)}`, context: silentErrors() } : undefined;
      },
      { parse: (body) => (body as ApiResponse<ProjectDetails>).data! },
    );
  }

  create(request: ProjectRequest): Observable<ProjectDetails> {
    return this.http
      .post<ApiResponse<ProjectDetails>>(baseUrl, request, { context: silentErrors() })
      .pipe(map((response) => response.data!));
  }

  update(id: string, request: ProjectRequest): Observable<ProjectDetails> {
    return this.http
      .put<ApiResponse<ProjectDetails>>(`${baseUrl}/${id}`, request, { context: silentErrors() })
      .pipe(map((response) => response.data!));
  }

  /** Soft-deletes the project and its tasks. */
  delete(id: string): Observable<void> {
    return this.http.delete<unknown>(`${baseUrl}/${id}`, { context: silentErrors() }).pipe(map(() => undefined));
  }

  addMember(projectId: string, userId: string): Observable<ProjectDetails> {
    return this.http
      .post<ApiResponse<ProjectDetails>>(`${baseUrl}/${projectId}/members`, { userId }, { context: silentErrors() })
      .pipe(map((response) => response.data!));
  }

  /** The member's tasks in this project become unassigned. */
  removeMember(projectId: string, userId: string): Observable<ProjectDetails> {
    return this.http
      .delete<ApiResponse<ProjectDetails>>(`${baseUrl}/${projectId}/members/${userId}`, { context: silentErrors() })
      .pipe(map((response) => response.data!));
  }
}

function toQueryParams(state: ProjectListState): Record<string, string | number | boolean> {
  const params: Record<string, string | number | boolean> = {
    sortBy: state.sortBy,
    sortDirection: state.sortDirection === 'desc' ? 'Desc' : 'Asc',
    page: state.page,
    pageSize: state.pageSize,
  };
  if (state.search) {
    params['search'] = state.search;
  }
  if (state.owner) {
    params['ownedByMe'] = state.owner === 'mine';
  }
  if (state.createdFrom) {
    params['createdFrom'] = state.createdFrom;
  }
  if (state.createdTo) {
    params['createdTo'] = state.createdTo;
  }
  return params;
}
