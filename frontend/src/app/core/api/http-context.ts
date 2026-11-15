import { HttpContext, HttpContextToken } from '@angular/common/http';

/** Set on requests whose failures the caller handles silently (no error snackbar). */
export const SKIP_ERROR_NOTIFICATION = new HttpContextToken<boolean>(() => false);

export const silentErrors = (): HttpContext => new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
