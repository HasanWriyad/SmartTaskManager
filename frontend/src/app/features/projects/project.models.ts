/** GET /api/projects item. canManage: the caller may edit, delete and change members. */
export interface ProjectListItem {
  id: string;
  name: string;
  description: string | null;
  ownerId: string;
  ownerName: string;
  memberCount: number;
  taskCount: number;
  canManage: boolean;
  createdAt: string;
}

/** GET /api/projects/{id}. */
export interface ProjectDetails {
  id: string;
  name: string;
  description: string | null;
  ownerId: string;
  ownerName: string;
  members: ProjectMember[];
  taskCount: number;
  canManage: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface ProjectMember {
  userId: string;
  fullName: string;
  email: string;
  addedAt: string;
}

export interface ProjectRequest {
  name: string;
  description: string | null;
}

/** Field limits from the backend's Project entity. */
export const projectNameMaxLength = 200;
export const projectDescriptionMaxLength = 2000;

export type ProjectSortField = 'name' | 'createdAt' | 'taskCount';
export type SortDirection = 'asc' | 'desc';
/** '' = all projects, 'mine' = ownedByMe=true, 'others' = ownedByMe=false. */
export type OwnerFilter = '' | 'mine' | 'others';

export const projectSortLabels: Record<ProjectSortField, string> = {
  name: 'Name',
  createdAt: 'Created',
  taskCount: 'Tasks',
};

/** Project list state; it lives in the URL query string. */
export interface ProjectListState {
  search: string;
  owner: OwnerFilter;
  /** yyyy-mm-dd, inclusive. */
  createdFrom: string;
  createdTo: string;
  sortBy: ProjectSortField;
  sortDirection: SortDirection;
  page: number;
  pageSize: number;
}

export const projectPageSizes = [5, 10, 25];

export const defaultProjectListState: ProjectListState = {
  search: '',
  owner: '',
  createdFrom: '',
  createdTo: '',
  sortBy: 'name',
  sortDirection: 'asc',
  page: 1,
  pageSize: 10,
};
