import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ApiError } from '../../core/api/api-error';
import { ApiResponse } from '../../core/api/api.models';
import { silentErrors } from '../../core/api/http-context';
import { Dashboard } from './dashboard.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  /**
   * The dashboard as a resource (value, isLoading, error, reload). Call from an injection context,
   * e.g. a component field. Errors are shown inline by the page, so the global snackbar is skipped.
   */
  load() {
    return httpResource<Dashboard>(() => ({ url: '/api/dashboard', context: silentErrors() }), {
      parse: (body) => (body as ApiResponse<Dashboard>).data!,
    });
  }

  /** A user-safe message for a failed load. */
  static errorMessage(error: unknown): string {
    if (error instanceof ApiError) {
      return error.message;
    }
    if (error instanceof HttpErrorResponse) {
      return ApiError.fromHttpError(error).message;
    }
    return 'The dashboard could not be loaded.';
  }
}
