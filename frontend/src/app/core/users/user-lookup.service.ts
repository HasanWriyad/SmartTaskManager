import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { ApiResponse, PagedResult } from '../api/api.models';
import { silentErrors } from '../api/http-context';

/** GET /api/users/lookup item: an active user to pick as a project member or assignee. */
export interface UserLookup {
  id: string;
  fullName: string;
  email: string;
}

/** Read-only user search for pickers (Admin and ProjectManager only; the API enforces it). */
@Injectable({ providedIn: 'root' })
export class UserLookupService {
  private readonly http = inject(HttpClient);

  /** Active users whose name or email matches; an empty list on any failure. */
  search(term: string, pageSize = 10): Observable<UserLookup[]> {
    const params: Record<string, string | number> = { sortBy: 'name', pageSize };
    if (term.trim()) {
      params['search'] = term.trim();
    }
    return this.http
      .get<ApiResponse<PagedResult<UserLookup>>>('/api/users/lookup', { params, context: silentErrors() })
      .pipe(
        map((response) => response.data?.items ?? []),
        catchError(() => of([])),
      );
  }
}
