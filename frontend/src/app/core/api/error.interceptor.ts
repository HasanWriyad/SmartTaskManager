import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from '../notifications/notification.service';
import { ApiError } from './api-error';
import { SKIP_ERROR_NOTIFICATION } from './http-context';

/**
 * Turns HTTP failures into ApiError (status, server message, field errors, trace id) and shows a
 * snackbar for the failures a page cannot reasonably handle itself: no connection, forbidden,
 * too large, rate limited, and server errors. 400/401/404/409/422 are left to the caller, which
 * shows them in context (form fields, "not found" states). Registered before authInterceptor, so
 * it sees the final outcome after any refresh-and-retry.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(NotificationService);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      const apiError = ApiError.fromHttpError(error);
      const notify = apiError.status === 0 || [403, 413, 429].includes(apiError.status) || apiError.status >= 500;
      if (notify && !request.context.get(SKIP_ERROR_NOTIFICATION)) {
        notifications.error(apiError.message);
      }
      return throwError(() => apiError);
    }),
  );
};
