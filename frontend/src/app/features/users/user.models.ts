import { Role } from '../../core/auth/auth.models';

/** GET /api/users item (Admin only). */
export interface AdminUser {
  id: string;
  email: string;
  fullName: string;
  role: Role;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export type UserSortField = 'name' | 'email' | 'role' | 'createdAt';
/** '' = everyone, 'active' = isActive=true, 'inactive' = isActive=false. */
export type StatusFilter = '' | 'active' | 'inactive';

/** Users list state; it lives in the URL query string. */
export interface UserListState {
  search: string;
  role: Role | '';
  status: StatusFilter;
  sortBy: UserSortField;
  sortDirection: 'asc' | 'desc';
  page: number;
  pageSize: number;
}

export const userPageSizes = [5, 10, 25];

export const userSortLabels: Record<UserSortField, string> = {
  name: 'Name',
  email: 'Email',
  role: 'Role',
  createdAt: 'Joined',
};

export const defaultUserListState: UserListState = {
  search: '',
  role: '',
  status: '',
  sortBy: 'name',
  sortDirection: 'asc',
  page: 1,
  pageSize: 10,
};
