import { BreakpointObserver } from '@angular/cdk/layout';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, debounceTime, distinctUntilChanged, filter, map, of, switchMap } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { PagedResult } from '../../core/api/api.models';
import { Role, roleLabels } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/notifications/notification.service';
import { Avatar } from '../../shared/avatar/avatar';
import { ConfirmService } from '../../shared/confirm-dialog/confirm-dialog';
import { providePaginatorLabels } from '../../shared/paginator/paginator-intl';
import { SELECT_IMPORTS } from '../../shared/select/select-panel-fit';
import { AdminUser, UserListState, UserSortField, userPageSizes, userSortLabels } from './user.models';
import { UserService, parseUserListState, userListQueryParams } from './user.service';

const phoneQuery = '(max-width: 719.98px)';
const mediumQuery = '(min-width: 720px) and (max-width: 959.98px)';
const roles: Role[] = ['Admin', 'ProjectManager', 'TeamMember'];

/**
 * User management (Admin only: roleGuard and the API). Search, role and status filters, sorting and
 * paging in the URL; change a user's role and activate or deactivate them. The API forbids changing your
 * own role or deactivating yourself (those actions are disabled here) and demoting or deactivating the
 * last active Admin (its message is shown).
 */
@Component({
  selector: 'app-users',
  imports: [
    DatePipe,
    NgTemplateOutlet,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSortModule,
    MatTableModule,
    SELECT_IMPORTS,
    Avatar,
  ],
  providers: [providePaginatorLabels()],
  templateUrl: './users.html',
  styleUrl: './users.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Users {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(UserService);
  private readonly auth = inject(AuthService);
  private readonly confirmService = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);

  protected readonly state = toSignal(this.route.queryParamMap.pipe(map(parseUserListState)), {
    initialValue: parseUserListState(this.route.snapshot.queryParamMap),
  });
  protected readonly users = this.api.list(this.state);
  protected readonly result = linkedSignal<PagedResult<AdminUser> | undefined, PagedResult<AdminUser> | undefined>({
    source: () => (this.users.hasValue() ? this.users.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });
  protected readonly errorMessage = computed(() => {
    const error = this.users.error();
    return error ? apiErrorMessage(error, 'The users could not be loaded.') : null;
  });

  protected readonly isPhone = toSignal(inject(BreakpointObserver).observe(phoneQuery).pipe(map((r) => r.matches)), {
    initialValue: inject(BreakpointObserver).isMatched(phoneQuery),
  });
  protected readonly currentUserId = computed(() => this.auth.user()?.id ?? '');
  protected readonly filtersActive = computed(() => {
    const s = this.state();
    return !!(s.search || s.role || s.status);
  });
  /** The user whose change is in flight (one at a time). */
  protected readonly busyId = signal<string | null>(null);

  protected readonly roles = roles;
  protected readonly roleLabels = roleLabels;
  /** 720-959px (icon rail): Joined is left out so the row actions stay on screen. */
  protected readonly isMedium = toSignal(inject(BreakpointObserver).observe(mediumQuery).pipe(map((r) => r.matches)), {
    initialValue: inject(BreakpointObserver).isMatched(mediumQuery),
  });
  protected readonly columns = computed(() =>
    this.isMedium() ? ['user', 'role', 'status', 'actions'] : ['user', 'role', 'status', 'createdAt', 'actions'],
  );
  protected readonly pageSizes = userPageSizes;
  protected readonly sortLabels = userSortLabels;
  protected readonly sortFields = Object.keys(userSortLabels) as UserSortField[];
  protected readonly skeletonRows = [1, 2, 3, 4, 5];

  protected readonly search = new FormControl(this.state().search, { nonNullable: true });

  constructor() {
    this.search.valueChanges
      .pipe(
        map((value) => value.trim()),
        debounceTime(300),
        distinctUntilChanged(),
        filter((value) => value !== this.state().search),
        takeUntilDestroyed(),
      )
      .subscribe((search) => this.update({ search }, { replaceUrl: true }));

    effect(() => {
      const search = this.state().search;
      if (this.search.value.trim() !== search) {
        this.search.setValue(search, { emitEvent: false });
      }
    });

    effect(() => {
      const result = this.result();
      if (result && result.items.length === 0 && result.totalCount > 0 && this.state().page > result.totalPages) {
        this.update({ page: result.totalPages }, { replaceUrl: true });
      }
    });
  }

  protected roleLabel(role: Role): string {
    return roleLabels[role];
  }

  protected isSelf(user: AdminUser): boolean {
    return user.id === this.currentUserId();
  }

  // ---------- State changes
  protected update(patch: Partial<UserListState>, options: { replaceUrl?: boolean } = {}): void {
    const next = { ...this.state(), ...patch };
    if (!('page' in patch)) {
      next.page = 1;
    }
    void this.router.navigate([], { relativeTo: this.route, queryParams: userListQueryParams(next), replaceUrl: options.replaceUrl ?? false });
  }

  protected clearFilters(): void {
    this.update({ search: '', role: '', status: '' });
  }

  protected sortChange(sort: Sort): void {
    this.update({ sortBy: sort.active as UserSortField, sortDirection: sort.direction || 'asc' });
  }

  protected sortBy(field: UserSortField): void {
    const s = this.state();
    const direction = s.sortBy === field ? (s.sortDirection === 'asc' ? 'desc' : 'asc') : field === 'createdAt' ? 'desc' : 'asc';
    this.update({ sortBy: field, sortDirection: direction });
  }

  protected pageChange(event: PageEvent): void {
    const pageSize = event.pageSize;
    this.update({ pageSize, page: pageSize === this.state().pageSize ? event.pageIndex + 1 : 1 });
  }

  // ---------- Actions
  protected changeRole(user: AdminUser, role: Role): void {
    if (this.isSelf(user) || user.role === role) {
      return;
    }
    const losesAdmin = user.role === 'Admin';
    this.act(
      user,
      {
        title: 'Change role?',
        message: `${user.fullName} will become a ${roleLabels[role]}.`,
        detail: `This takes effect immediately${losesAdmin ? '; they lose access to user management' : ''}.`,
        confirmLabel: 'Change role',
      },
      () => this.api.changeRole(user.id, role),
      `${user.fullName} is now a ${roleLabels[role]}.`,
    );
  }

  protected setActive(user: AdminUser, isActive: boolean): void {
    if (this.isSelf(user) && !isActive) {
      return;
    }
    if (isActive) {
      this.act(user, null, () => this.api.changeStatus(user.id, true), `${user.fullName} can sign in again.`);
      return;
    }
    this.act(
      user,
      {
        title: 'Deactivate user?',
        message: `${user.fullName} will be signed out right away and can't sign in until an Admin activates them again.`,
        confirmLabel: 'Deactivate',
        destructive: true,
      },
      () => this.api.changeStatus(user.id, false),
      `${user.fullName} has been deactivated.`,
    );
  }

  /** Optional confirmation, then the change; errors show the API's message. */
  private act(
    user: AdminUser,
    confirm: Parameters<ConfirmService['confirm']>[0] | null,
    change: () => Observable<AdminUser>,
    successMessage: string,
  ): void {
    if (this.busyId()) {
      return;
    }
    (confirm ? this.confirmService.confirm(confirm) : of(true))
      .pipe(
        filter(Boolean),
        switchMap(() => {
          this.busyId.set(user.id);
          return change();
        }),
      )
      .subscribe({
        next: () => {
          this.busyId.set(null);
          this.notifications.success(successMessage);
          this.users.reload();
        },
        error: (error: unknown) => {
          this.busyId.set(null);
          this.notifications.error(apiErrorMessage(error, 'The change could not be saved.'));
        },
      });
  }

  protected retry(): void {
    this.users.reload();
  }
}
