import { HttpErrorResponse } from '@angular/common/http';
import { ApiResponse } from './api.models';

const fallbackMessages: Record<number, string> = {
  0: 'Cannot reach the server. Check your connection and try again.',
  400: 'The request is invalid.',
  401: 'Your session has expired. Please sign in again.',
  403: 'You do not have permission to perform this action.',
  404: 'The requested item was not found.',
  409: 'The request conflicts with existing data.',
  413: 'The request is too large.',
  429: 'Too many requests. Please try again shortly.',
};

/**
 * An API failure in a form the UI can use: HTTP status, a user-safe message from the server's
 * ApiResponse, field errors (camelCase keys) and the trace id for support.
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly fieldErrors: Record<string, string[]> = {},
    readonly traceId: string | null = null,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  static fromHttpError(error: HttpErrorResponse): ApiError {
    const fallback = fallbackMessages[error.status]
      ?? (error.status >= 500 ? 'Something went wrong on the server. Please try again.' : 'The request failed.');
    const body = error.error as Partial<ApiResponse<unknown>> | null;

    if (error.status !== 0 && body !== null && typeof body === 'object') {
      return new ApiError(error.status, body.message ?? fallback, body.errors ?? {}, body.traceId ?? null);
    }
    return new ApiError(error.status, fallback);
  }
}
