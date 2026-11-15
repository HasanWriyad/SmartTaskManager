import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, filter, map, switchMap, tap } from 'rxjs';
import { apiErrorMessage, apiErrorStatus } from '../../../core/api/api-error';
import { AuthService } from '../../../core/auth/auth.service';
import { NotificationService } from '../../../core/notifications/notification.service';
import { UserLookup, UserLookupService } from '../../../core/users/user-lookup.service';
import { Avatar } from '../../../shared/avatar/avatar';
import { ConfirmService } from '../../../shared/confirm-dialog/confirm-dialog';
import { ProjectActions } from '../project-actions';
import { ProjectTasks } from './project-tasks';
import { ProjectDetails as Project, ProjectMember } from '../project.models';
import { ProjectService } from '../project.service';

/**
 * One project: name, description, owner, task count and members. Users with canManage can edit,
 * delete, and add members (from the user lookup) or remove them; the owner cannot be removed.
 */
@Component({
  selector: 'app-project-details',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    MatAutocompleteModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTooltipModule,
    Avatar,
    ProjectTasks,
  ],
  templateUrl: './project-details.html',
  styleUrl: './project-details.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectDetailsPage {
  private readonly api = inject(ProjectService);
  private readonly actions = inject(ProjectActions);
  private readonly lookup = inject(UserLookupService);
  private readonly confirmService = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly title = inject(Title);

  /** Route parameter :id. */
  readonly id = input.required<string>();

  protected readonly project = this.api.details(() => this.id());
  protected readonly data = computed(() => (this.project.hasValue() ? this.project.value() : undefined));
  protected readonly notFound = computed(() => {
    const status = apiErrorStatus(this.project.error());
    // 404: missing or not visible (the API hides it); 400: the id is not a GUID.
    return status === 404 || status === 400;
  });
  protected readonly errorMessage = computed(() => {
    const error = this.project.error();
    return error ? apiErrorMessage(error, 'The project could not be loaded.') : null;
  });

  protected readonly currentUserId = computed(() => this.auth.user()?.id ?? null);
  /** Owner first, then by name. */
  protected readonly members = computed(() => {
    const project = this.data();
    if (!project) {
      return [];
    }
    return [...project.members].sort(
      (a, b) => Number(b.userId === project.ownerId) - Number(a.userId === project.ownerId) || a.fullName.localeCompare(b.fullName),
    );
  });

  /** Member add/remove in flight (one at a time). */
  protected readonly busy = signal(false);

  protected readonly memberSearch = new FormControl<string | UserLookup>('', { nonNullable: true });
  protected readonly candidates = signal<UserLookup[]>([]);
  protected readonly searching = signal(false);

  constructor() {
    // Tab title: the project's name once loaded.
    effect(() => {
      const name = this.data()?.name;
      if (name) {
        this.title.setTitle(`${name} · SmartTask`);
      }
    });

    // User lookup for "Add member" (only for users who can manage; the endpoint is Admin/PM only):
    // debounced search; existing members are left out by `available`.
    const term = toSignal(
      this.memberSearch.valueChanges.pipe(
        filter((value): value is string => typeof value === 'string'),
        map((value) => value.trim()),
        debounceTime(250),
      ),
      { initialValue: '' },
    );
    toObservable(computed(() => ({ canManage: !!this.data()?.canManage, term: term() })))
      .pipe(
        filter((query) => query.canManage),
        distinctUntilChanged((a, b) => a.term === b.term),
        tap(() => this.searching.set(true)),
        switchMap((query) => this.lookup.search(query.term, 20)),
        takeUntilDestroyed(),
      )
      .subscribe((users) => {
        this.searching.set(false);
        this.candidates.set(users);
      });
  }

  protected readonly available = computed(() => {
    const members = new Set(this.data()?.members.map((m) => m.userId) ?? []);
    return this.candidates().filter((user) => !members.has(user.id));
  });

  protected displayUser(value: string | UserLookup | null): string {
    return typeof value === 'string' ? value : (value?.fullName ?? '');
  }

  protected addMember(event: MatAutocompleteSelectedEvent): void {
    const user = event.option.value as UserLookup;
    const project = this.data();
    this.memberSearch.setValue('', { emitEvent: false });
    if (!project || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.api.addMember(project.id, user.id).subscribe({
      next: (updated) => {
        this.busy.set(false);
        this.project.set(updated);
        this.notifications.success(`${user.fullName} added to the project.`);
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.notifications.error(apiErrorMessage(error, 'The member could not be added.'));
      },
    });
  }

  protected removeMember(member: ProjectMember): void {
    const project = this.data();
    if (!project || this.busy()) {
      return;
    }
    this.confirmService
      .confirm({
        title: 'Remove member?',
        message: `${member.fullName} will be removed from "${project.name}".`,
        detail: 'Their tasks in this project become unassigned.',
        confirmLabel: 'Remove member',
        destructive: true,
      })
      .pipe(
        filter(Boolean),
        tap(() => this.busy.set(true)),
        switchMap(() => this.api.removeMember(project.id, member.userId)),
      )
      .subscribe({
        next: (updated) => {
          this.busy.set(false);
          this.project.set(updated);
          this.notifications.success(`${member.fullName} removed from the project.`);
        },
        error: (error: unknown) => {
          this.busy.set(false);
          this.notifications.error(apiErrorMessage(error, 'The member could not be removed.'));
        },
      });
  }

  protected edit(project: Project): void {
    this.actions.edit(project).subscribe((saved) => this.project.set(saved));
  }

  protected delete(project: Project): void {
    this.actions.delete(project).subscribe(() => void this.router.navigate(['/projects']));
  }

  /** A task was created, edited or deleted in the Tasks section: refresh the task count. */
  protected tasksChanged(): void {
    this.project.reload();
  }

  protected retry(): void {
    this.project.reload();
  }
}
