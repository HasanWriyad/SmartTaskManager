import { AbstractControl, FormGroup } from '@angular/forms';
import { ApiError } from '../api/api-error';

/**
 * Puts the API's field errors (camelCase keys, as the backend returns them) on the matching form
 * controls as a `server` error. Returns true when at least one field error was applied.
 */
export function applyServerErrors(form: FormGroup, error: ApiError): boolean {
  let applied = false;
  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    const control: AbstractControl | null = form.get(field);
    if (control && messages.length > 0) {
      control.setErrors({ ...control.errors, server: messages[0] });
      control.markAsTouched();
      applied = true;
    }
  }
  return applied;
}

/**
 * Only same-app paths are allowed as a post-login redirect target ("/projects", not "//evil.com" or
 * "https://..."), so a crafted link cannot send the user to another site.
 */
export function safeReturnUrl(value: string | null | undefined, fallback = '/dashboard'): string {
  return value && value.startsWith('/') && !value.startsWith('//') ? value : fallback;
}
