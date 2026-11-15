import { BreakpointObserver } from '@angular/cdk/layout';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, linkedSignal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideAppDateAdapter } from '../../shared/dates/app-date-adapter';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, filter, map } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { PagedResult } from '../../core/api/api.models';
import { AuthService } from '../../core/auth/auth.service';
import { HeaderAction } from '../../layout/header-action/header-action';
import { Avatar } from '../../shared/avatar/avatar';
import { providePaginatorLabels } from '../../shared/paginator/paginator-intl';
import { ProjectActions } from './project-actions';
import {
  ProjectListItem,
  ProjectListState,
  ProjectSortField,
  projectPageSizes,
  projectSortLabels,
} from './project.models';
import { ProjectService } from './project.service';
import { fromIsoDate, parseProjectListState, projectListQueryParams, toIsoDate } from './project-list.state';

const phoneQuery = '(max-width: 719.98px)';

/**
 * Project list: search, owner filter, created date range, sorting and paging, all kept in the URL.
 * A table at >= 720px and cards below. "New project" (header) for Admin and ProjectManager; edit and
 * delete per row when the API says canManage.
 */
@Component({
  selector: 'app-projects',
  imports: [
    DatePipe,
    NgTemplateOutlet,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    Avatar,
    HeaderAction,
  ],
  providers: [provideAppDateAdapter(), providePaginatorLabels()],
  templateUrl: './projects.html',
  styleUrl: './projects.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Projects {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly actions = inject(ProjectActions);

  /** Admin and ProjectManager create (and so own) projects; TeamMembers never do. */
  protected readonly canCreate = computed(() => this.auth.hasAnyRole(['Admin', 'ProjectManager']));

  private readonly urlState = toSignal(this.route.queryParamMap.pipe(map(parseProjectListState)), {
    initialValue: parseProjectListState(this.route.snapshot.queryParamMap),
  });
  /** List state from the URL. TeamMembers have no owner filter, so an ownedByMe in the URL is ignored. */
  protected readonly state = computed(() => {
    const state = this.urlState();
    return this.canCreate() || !state.owner ? state : { ...state, owner: '' as const };
  });
  protected readonly projects = inject(ProjectService).list(this.state);

  /** The latest loaded page; kept while the next one loads, so the list does not flash. */
  protected readonly result = linkedSignal<PagedResult<ProjectListItem> | undefined, PagedResult<ProjectListItem> | undefined>({
    source: () => (this.projects.hasValue() ? this.projects.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });
  protected readonly errorMessage = computed(() => {
    const error = this.projects.error();
    return error ? apiErrorMessage(error, 'The projects could not be loaded.') : null;
  });

  protected readonly isPhone = toSignal(inject(BreakpointObserver).observe(phoneQuery).pipe(map((r) => r.matches)), {
    initialValue: inject(BreakpointObserver).isMatched(phoneQuery),
  });
  protected readonly currentUserId = computed(() => this.auth.user()?.id ?? null);
  protected readonly filtersActive = computed(() => {
    const s = this.state();
    return !!(s.search || s.owner || s.createdFrom || s.createdTo);
  });

  /** The actions column only when the user can manage a project on this page (canManage from the API). */
  protected readonly columns = computed(() => {
    const columns = ['name', 'owner', 'members', 'taskCount', 'createdAt'];
    return this.result()?.items.some((p) => p.canManage) ? [...columns, 'actions'] : columns;
  });
  protected readonly pageSizes = projectPageSizes;
  protected readonly sortLabels = projectSortLabels;
  protected readonly sortFields = Object.keys(projectSortLabels) as ProjectSortField[];
  protected readonly skeletonRows = [1, 2, 3, 4, 5];

  protected readonly search = new FormControl(this.state().search, { nonNullable: true });
  protected readonly range = new FormGroup({
    start: new FormControl<Date | null>(fromIsoDate(this.state().createdFrom)),
    end: new FormControl<Date | null>(fromIsoDate(this.state().createdTo)),
  });

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

    this.range.valueChanges
      .pipe(
        debounceTime(300),
        map(({ start, end }) => ({ createdFrom: toIsoDate(start ?? null), createdTo: toIsoDate(end ?? null) })),
        filter(({ createdFrom, createdTo }) => createdFrom !== this.state().createdFrom || createdTo !== this.state().createdTo),
        takeUntilDestroyed(),
      )
      .subscribe((dates) => this.update(dates));

    // URL -> fields, for Back/Forward and "Clear filters".
    effect(() => {
      const s = this.state();
      if (this.search.value.trim() !== s.search) {
        this.search.setValue(s.search, { emitEvent: false });
      }
      if (toIsoDate(this.range.value.start ?? null) !== s.createdFrom || toIsoDate(this.range.value.end ?? null) !== s.createdTo) {
        this.range.setValue({ start: fromIsoDate(s.createdFrom), end: fromIsoDate(s.createdTo) }, { emitEvent: false });
      }
    });

    // A page past the end (e.g. after deleting the last project on it): go to the last page.
    effect(() => {
      const result = this.result();
      if (result && result.items.length === 0 && result.totalCount > 0 && this.state().page > result.totalPages) {
        this.update({ page: result.totalPages }, { replaceUrl: true });
      }
    });
  }

  protected update(patch: Partial<ProjectListState>, options: { replaceUrl?: boolean } = {}): void {
    const next = { ...this.state(), ...patch };
    if (!('page' in patch)) {
      next.page = 1;
    }
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: projectListQueryParams(next),
      replaceUrl: options.replaceUrl ?? false,
    });
  }

  protected clearFilters(): void {
    this.update({ search: '', owner: '', createdFrom: '', createdTo: '' });
  }

  protected sortChange(sort: Sort): void {
    this.update({ sortBy: sort.active as ProjectSortField, sortDirection: sort.direction || 'asc' });
  }

  /** Phone sort menu: picking the current field flips the direction. */
  protected sortBy(field: ProjectSortField): void {
    const s = this.state();
    const direction = s.sortBy === field ? (s.sortDirection === 'asc' ? 'desc' : 'asc') : field === 'name' ? 'asc' : 'desc';
    this.update({ sortBy: field, sortDirection: direction });
  }

  protected pageChange(event: PageEvent): void {
    const pageSize = event.pageSize;
    this.update({ pageSize, page: pageSize === this.state().pageSize ? event.pageIndex + 1 : 1 });
  }

  protected open(project: ProjectListItem): void {
    void this.router.navigate(['/projects', project.id]);
  }

  protected create(): void {
    this.actions.create();
  }

  protected edit(project: ProjectListItem): void {
    this.actions.edit(project).subscribe(() => this.projects.reload());
  }

  protected delete(project: ProjectListItem): void {
    this.actions.delete(project).subscribe(() => this.projects.reload());
  }

  protected retry(): void {
    this.projects.reload();
  }
}
