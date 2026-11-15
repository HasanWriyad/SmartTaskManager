/** Envelope of every API response (success and error), mirroring the backend's ApiResponse<T>. */
export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  message: string | null;
  /** Validation errors keyed by camelCase field name. */
  errors: Record<string, string[]> | null;
  traceId: string | null;
}

/** One page of a list, mirroring the backend's PagedResult<T>. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}
