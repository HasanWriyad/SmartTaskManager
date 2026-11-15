import { ParamMap, Params } from '@angular/router';
import {
  OwnerFilter,
  ProjectListState,
  ProjectSortField,
  SortDirection,
  defaultProjectListState as defaults,
  projectPageSizes,
} from './project.models';

const sortFields: ProjectSortField[] = ['name', 'createdAt', 'taskCount'];
const isoDate = /^\d{4}-\d{2}-\d{2}$/;

/** Reads the list state from the URL; anything missing or invalid falls back to the default. */
export function parseProjectListState(query: ParamMap): ProjectListState {
  const sortBy = query.get('sortBy') as ProjectSortField | null;
  const sortDirection = query.get('sortDirection') as SortDirection | null;
  const ownedByMe = query.get('ownedByMe');
  const page = Number(query.get('page'));
  const pageSize = Number(query.get('pageSize'));
  let createdFrom = validDate(query.get('createdFrom'));
  let createdTo = validDate(query.get('createdTo'));
  if (createdFrom && createdTo && createdTo < createdFrom) {
    [createdFrom, createdTo] = [createdTo, createdFrom];
  }

  return {
    search: (query.get('search') ?? '').trim().slice(0, 100),
    owner: ownedByMe === 'true' ? 'mine' : ownedByMe === 'false' ? 'others' : '',
    createdFrom,
    createdTo,
    sortBy: sortBy && sortFields.includes(sortBy) ? sortBy : defaults.sortBy,
    sortDirection: sortDirection === 'asc' || sortDirection === 'desc' ? sortDirection : defaults.sortDirection,
    page: Number.isInteger(page) && page >= 1 ? page : defaults.page,
    pageSize: projectPageSizes.includes(pageSize) ? pageSize : defaults.pageSize,
  };
}

/** The URL query for a state: only values that differ from the default, so links stay short. */
export function projectListQueryParams(state: ProjectListState): Params {
  const ownedByMe: Record<OwnerFilter, string | null> = { '': null, mine: 'true', others: 'false' };
  return {
    search: state.search || null,
    ownedByMe: ownedByMe[state.owner],
    createdFrom: state.createdFrom || null,
    createdTo: state.createdTo || null,
    sortBy: state.sortBy !== defaults.sortBy ? state.sortBy : null,
    sortDirection: state.sortDirection !== defaults.sortDirection ? state.sortDirection : null,
    page: state.page !== defaults.page ? state.page : null,
    pageSize: state.pageSize !== defaults.pageSize ? state.pageSize : null,
  };
}

/** Local calendar date to yyyy-mm-dd (the date the user picked, not shifted to UTC). */
export function toIsoDate(date: Date | null): string {
  if (!date || Number.isNaN(date.getTime())) {
    return '';
  }
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** yyyy-mm-dd to a local Date for the datepicker. */
export function fromIsoDate(value: string): Date | null {
  if (!value) {
    return null;
  }
  const [y, m, d] = value.split('-').map(Number);
  return new Date(y, m - 1, d);
}

function validDate(value: string | null): string {
  return value && isoDate.test(value) && toIsoDate(fromIsoDate(value)) === value ? value : '';
}
