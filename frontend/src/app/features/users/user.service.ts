import { HttpClient, httpResource } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { ParamMap, Params } from '@angular/router';
import { Observable, map } from 'rxjs';
import { ApiResponse, PagedResult } from '../../core/api/api.models';
import { silentErrors } from '../../core/api/http-context';
import { Role } from '../../core/auth/auth.models';
import { AdminUser, StatusFilter, UserListState, UserSortField, defaultUserListState as defaults, userPageSizes } from './user.models';

/**
 * User management API (Admin only; the API enforces it). Every request skips the global error snackbar:
 * the page shows failures itself, with the API's message (e.g. the last active Admin cannot be demoted).
 */
@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly http = inject(HttpClient);

  /** GET /api/users for the given state. Call from an injection context. */
  list(state: () => UserListState) {
    return httpResource<PagedResult<AdminUser>>(
      () => ({ url: '/api/users', params: toQueryParams(state()), context: silentErrors() }),
      { parse: (body) => (body as ApiResponse<PagedResult<AdminUser>>).data! },
    );
  }

  /** Takes effect immediately: the user's next request gets a new token with the new role. */
  changeRole(userId: string, role: Role): Observable<AdminUser> {
    return this.http
      .put<ApiResponse<AdminUser>>(`/api/users/${userId}/role`, { role }, { context: silentErrors() })
      .pipe(map((response) => response.data!));
  }

  /** Deactivating ends the user's sessions immediately. */
  changeStatus(userId: string, isActive: boolean): Observable<AdminUser> {
    return this.http
      .put<ApiResponse<AdminUser>>(`/api/users/${userId}/status`, { isActive }, { context: silentErrors() })
      .pipe(map((response) => response.data!));
  }
}

function toQueryParams(state: UserListState): Record<string, string | number | boolean> {
  const params: Record<string, string | number | boolean> = {
    sortBy: state.sortBy,
    sortDirection: state.sortDirection === 'desc' ? 'Desc' : 'Asc',
    page: state.page,
    pageSize: state.pageSize,
  };
  if (state.search) {
    params['search'] = state.search;
  }
  if (state.role) {
    params['role'] = state.role;
  }
  if (state.status) {
    params['isActive'] = state.status === 'active';
  }
  return params;
}

const roles: Role[] = ['Admin', 'ProjectManager', 'TeamMember'];
const sortFields: UserSortField[] = ['name', 'email', 'role', 'createdAt'];

/** Reads the users list state from the URL; anything missing or invalid falls back to the default. */
export function parseUserListState(query: ParamMap): UserListState {
  const role = query.get('role') as Role | null;
  const sortBy = query.get('sortBy') as UserSortField | null;
  const sortDirection = query.get('sortDirection');
  const isActive = query.get('isActive');
  const page = Number(query.get('page'));
  const pageSize = Number(query.get('pageSize'));
  return {
    search: (query.get('search') ?? '').trim().slice(0, 100),
    role: role && roles.includes(role) ? role : '',
    status: isActive === 'true' ? 'active' : isActive === 'false' ? 'inactive' : '',
    sortBy: sortBy && sortFields.includes(sortBy) ? sortBy : defaults.sortBy,
    sortDirection: sortDirection === 'asc' || sortDirection === 'desc' ? sortDirection : defaults.sortDirection,
    page: Number.isInteger(page) && page >= 1 ? page : defaults.page,
    pageSize: userPageSizes.includes(pageSize) ? pageSize : defaults.pageSize,
  };
}

/** The URL query for a state: only values that differ from the default. */
export function userListQueryParams(state: UserListState): Params {
  const isActive: Record<StatusFilter, string | null> = { '': null, active: 'true', inactive: 'false' };
  return {
    search: state.search || null,
    role: state.role || null,
    isActive: isActive[state.status],
    sortBy: state.sortBy !== defaults.sortBy ? state.sortBy : null,
    sortDirection: state.sortDirection !== defaults.sortDirection ? state.sortDirection : null,
    page: state.page !== defaults.page ? state.page : null,
    pageSize: state.pageSize !== defaults.pageSize ? state.pageSize : null,
  };
}
