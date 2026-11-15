import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiResponse } from '../api/api.models';
import { silentErrors } from '../api/http-context';

export interface ImproveDescriptionResponse {
  improvedDescription: string;
  /** "ai" from the model, "fallback" when no provider is configured. */
  source: 'ai' | 'fallback';
  model: string | null;
}

/**
 * POST /api/ai/improve-description (Admin and ProjectManager). Returns a suggestion only; nothing is
 * saved. Failures (429 rate limit, 502/503/504 provider errors) are shown by the caller, not the snackbar.
 */
@Injectable({ providedIn: 'root' })
export class AiService {
  private readonly http = inject(HttpClient);

  improveDescription(description: string, title: string | null): Observable<ImproveDescriptionResponse> {
    return this.http
      .post<ApiResponse<ImproveDescriptionResponse>>(
        '/api/ai/improve-description',
        { description, title: title || null },
        { context: silentErrors() },
      )
      .pipe(map((response) => response.data!));
  }
}
