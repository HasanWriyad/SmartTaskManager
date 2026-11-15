import { BreakpointObserver } from '@angular/cdk/layout';
import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, linkedSignal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { SelectPanelFit } from '../../shared/select/select-panel-fit';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router } from '@angular/router';
import { debounceTime, distinctUntilChanged, filter, map } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { ApiResponse, PagedResult } from '../../core/api/api.models';
import { silentErrors } from '../../core/api/http-context';
import { AuthService } from '../../core/auth/auth.service';
import { dueCaption, isOverdue, localToday } from '../../core/tasks/task-due';
import { TaskPriority, TaskStatus, taskPriorityLabels, taskStatusLabels } from '../../core/tasks/task.models';
import { UserLookup } from '../../core/users/user-lookup.service';
import { HeaderAction } from '../../layout/header-action/header-action';
import { Avatar } from '../../shared/avatar/avatar';
import { PriorityBadge } from '../../shared/badges/priority-badge';
import { StatusBadge } from '../../shared/badges/status-badge';
import { providePaginatorLabels } from '../../shared/paginator/paginator-intl';
import { defaultProjectListState } from '../projects/project.models';
import { ProjectService } from '../projects/project.service';
import { TaskDialogService } from './task-dialog/task-dialog';
import { hasTaskFilters, parseTaskListState, taskListQueryParams } from './task-list.state';
import {
  DueFilter,
  TaskListItem,
  TaskListState,
  TaskSortField,
  dueFilterLabels,
  taskPageSizes,
  taskSortLabels,
} from './task.models';
import { TaskService } from './task.service';

const phoneQuery = '(max-width: 719.98px)';

/** What the current user may do with a row: manage (edit/assign/delete), status only, or view. */
type RowAccess = 'manage' | 'status' | 'view';

/**
 * Task list (spec §7.3) on GET /api/tasks: search, project, status, priority, assignee and due-date
 * filters, sorting and paging, all kept in the URL (?projectId= comes from the project page).
 * A table at >= 720px, cards below. Rows open the task dialog.
 */
@Component({
  selector: 'app-tasks',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    SelectPanelFit,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
    Avatar,
    HeaderAction,
    PriorityBadge,
    StatusBadge,
  ],
  providers: [providePaginatorLabels()],
  templateUrl: './tasks.html',
  styleUrl: './tasks.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Tasks {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly dialogs = inject(TaskDialogService);
  private readonly projectsApi = inject(ProjectService);

  protected readonly state = toSignal(this.route.queryParamMap.pipe(map(parseTaskListState)), {
    initialValue: parseTaskListState(this.route.snapshot.queryParamMap),
  });
  protected readonly tasks = inject(TaskService).list(this.state);

  /** The latest loaded page; kept while the next one loads, so the list does not flash. */
  protected readonly result = linkedSignal<PagedResult<TaskListItem> | undefined, PagedResult<TaskListItem> | undefined>({
    source: () => (this.tasks.hasValue() ? this.tasks.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });
  protected readonly errorMessage = computed(() => {
    const error = this.tasks.error();
    return error ? apiErrorMessage(error, 'The tasks could not be loaded.') : null;
  });

  protected readonly isPhone = toSignal(inject(BreakpointObserver).observe(phoneQuery).pipe(map((r) => r.matches)), {
    initialValue: inject(BreakpointObserver).isMatched(phoneQuery),
  });
  protected readonly currentUserId = computed(() => this.auth.user()?.id ?? '');
  /** Admin and ProjectManager create tasks (in projects they manage) and can look users up. */
  protected readonly canCreate = computed(() => this.auth.hasAnyRole(['Admin', 'ProjectManager']));
  protected readonly filtersActive = computed(() => hasTaskFilters(this.state()));
  protected readonly today = localToday();

  // ---------- Filter options
  /** Projects the user can see (first 100 by name), for the project filter. */
  private readonly projects = this.projectsApi.list(() => ({ ...defaultProjectListState, pageSize: 100 }));
  protected readonly projectOptions = computed(() => (this.projects.hasValue() ? this.projects.value().items : []));

  /**
   * Assignee options: the chosen project's members; otherwise (Admin/PM) the user lookup. The requests
   * depend on these computed values, not on the whole state, so other filter changes don't refetch them.
   */
  private readonly filterProjectId = computed(() => this.state().projectId || undefined);
  private readonly useLookup = computed(() => !this.filterProjectId() && this.canCreate());
  private readonly projectDetails = this.projectsApi.details(() => this.filterProjectId());
  private readonly lookup = httpResource<UserLookup[]>(
    () =>
      this.useLookup()
        ? { url: '/api/users/lookup', params: { sortBy: 'name', pageSize: 50 }, context: silentErrors() }
        : undefined,
    { parse: (body) => (body as ApiResponse<PagedResult<UserLookup>>).data!.items },
  );
  protected readonly assigneeOptions = computed(() => {
    const me = this.currentUserId();
    const people = this.filterProjectId()
      ? (this.projectDetails.hasValue() ? this.projectDetails.value().members : []).map((m) => ({ id: m.userId, name: m.fullName }))
      : (this.lookup.hasValue() ? this.lookup.value() : []).map((u) => ({ id: u.id, name: u.fullName }));
    return people.filter((p) => p.id !== me).sort((a, b) => a.name.localeCompare(b.name));
  });
  /** A filtered assignee or project that is not among the options (e.g. from a shared link). */
  protected readonly unknownAssignee = computed(() => {
    const id = this.state().assigneeId;
    return !!id && id !== this.currentUserId() && !this.assigneeOptions().some((p) => p.id === id);
  });
  protected readonly unknownProject = computed(() => {
    const id = this.state().projectId;
    return !!id && this.projects.hasValue() && !this.projectOptions().some((p) => p.id === id);
  });

  protected readonly statuses: TaskStatus[] = ['ToDo', 'InProgress', 'Completed', 'Cancelled'];
  protected readonly priorities: TaskPriority[] = ['Critical', 'High', 'Medium', 'Low'];
  protected readonly dueFilters = Object.keys(dueFilterLabels) as Exclude<DueFilter, ''>[];
  protected readonly statusLabels = taskStatusLabels;
  protected readonly priorityLabels = taskPriorityLabels;
  protected readonly dueLabels = dueFilterLabels;
  protected readonly sortLabels = taskSortLabels;
  protected readonly sortFields = Object.keys(taskSortLabels) as TaskSortField[];
  protected readonly pageSizes = taskPageSizes;
  protected readonly columns = ['title', 'status', 'priority', 'assignee', 'dueDate', 'actions'];
  protected readonly skeletonRows = [1, 2, 3, 4, 5];

  protected readonly search = new FormControl(this.state().search, { nonNullable: true });

  constructor() {
    // Search is debounced by 300ms (spec §7.3) and replaces the history entry instead of adding one.
    this.search.valueChanges
      .pipe(
        map((value) => value.trim()),
        debounceTime(300),
        distinctUntilChanged(),
        filter((value) => value !== this.state().search),
        takeUntilDestroyed(),
      )
      .subscribe((search) => this.update({ search }, { replaceUrl: true }));

    // URL -> search box, for Back/Forward and "Clear filters".
    effect(() => {
      const search = this.state().search;
      if (this.search.value.trim() !== search) {
        this.search.setValue(search, { emitEvent: false });
      }
    });

    // A page past the end (e.g. after deleting the last task on it): go to the last page.
    effect(() => {
      const result = this.result();
      if (result && result.items.length === 0 && result.totalCount > 0 && this.state().page > result.totalPages) {
        this.update({ page: result.totalPages }, { replaceUrl: true });
      }
    });
  }

  // ---------- Row helpers
  /** The row action, from the API's canManage / canUpdateStatus on each row. */
  protected access(task: TaskListItem): RowAccess {
    if (task.canManage) {
      return 'manage';
    }
    return task.canUpdateStatus ? 'status' : 'view';
  }

  protected overdue(task: TaskListItem): boolean {
    return isOverdue(task.dueDate, task.status, this.today);
  }

  protected caption(task: TaskListItem): string {
    return task.dueDate ? dueCaption(task.dueDate, task.status, this.today) : '';
  }

  // ---------- State changes
  protected update(patch: Partial<TaskListState>, options: { replaceUrl?: boolean } = {}): void {
    const next = { ...this.state(), ...patch };
    if (!('page' in patch)) {
      next.page = 1;
    }
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: taskListQueryParams(next),
      replaceUrl: options.replaceUrl ?? false,
    });
  }

  protected setProject(projectId: string): void {
    // Assignee options depend on the project; a filtered assignee may not be a member there.
    this.update({ projectId, assigneeId: projectId ? '' : this.state().assigneeId });
  }

  protected clearFilters(): void {
    this.update({ search: '', status: '', priority: '', assigneeId: '', due: '', projectId: '' });
  }

  protected sortChange(sort: Sort): void {
    this.update({ sortBy: sort.active as TaskSortField, sortDirection: sort.direction || 'asc' });
  }

  /** Phone sort menu: picking the current field flips the direction. */
  protected sortBy(field: TaskSortField): void {
    const s = this.state();
    const direction = s.sortBy === field ? (s.sortDirection === 'asc' ? 'desc' : 'asc') : 'asc';
    this.update({ sortBy: field, sortDirection: direction });
  }

  protected pageChange(event: PageEvent): void {
    const pageSize = event.pageSize;
    this.update({ pageSize, page: pageSize === this.state().pageSize ? event.pageIndex + 1 : 1 });
  }

  // ---------- Dialog
  protected create(): void {
    this.dialogs.open({ projectId: this.state().projectId || undefined }).subscribe((result) => {
      if (result) {
        this.tasks.reload();
      }
    });
  }

  protected open(task: TaskListItem): void {
    this.dialogs.open({ projectId: task.projectId, taskId: task.id }).subscribe((result) => {
      if (result) {
        this.tasks.reload();
      }
    });
  }

  protected retry(): void {
    this.tasks.reload();
  }
}
