import { BreakpointObserver } from '@angular/cdk/layout';
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, linkedSignal, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { map } from 'rxjs';
import { apiErrorMessage } from '../../../core/api/api-error';
import { PagedResult } from '../../../core/api/api.models';
import { AuthService } from '../../../core/auth/auth.service';
import { dueCaption, isOverdue, utcToday } from '../../../core/tasks/task-due';
import { Avatar } from '../../../shared/avatar/avatar';
import { PriorityBadge } from '../../../shared/badges/priority-badge';
import { StatusBadge } from '../../../shared/badges/status-badge';
import { providePaginatorLabels } from '../../../shared/paginator/paginator-intl';
import { TaskDialogService } from '../../tasks/task-dialog/task-dialog';
import { TaskListItem } from '../../tasks/task.models';
import { TaskService } from '../../tasks/task.service';

const phoneQuery = '(max-width: 719.98px)';
const pageSizes = [5, 10, 25];

/**
 * The project's tasks on its details page (GET /api/projects/{id}/tasks, the API's default order),
 * with paging. Rows open the task dialog; with canManage a "New task" button opens the create dialog
 * with this project fixed. Emits `changed` after a create, edit or delete so the page can refresh the
 * project's task count.
 */
@Component({
  selector: 'app-project-tasks',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatPaginatorModule, MatProgressBarModule, Avatar, PriorityBadge, StatusBadge],
  providers: [providePaginatorLabels()],
  templateUrl: './project-tasks.html',
  styleUrl: './project-tasks.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectTasks {
  private readonly dialogs = inject(TaskDialogService);
  private readonly auth = inject(AuthService);

  readonly projectId = input.required<string>();
  readonly projectName = input.required<string>();
  readonly canManage = input(false);
  readonly changed = output<void>();

  protected readonly page = signal(1);
  protected readonly pageSize = signal(10);
  protected readonly pageSizes = pageSizes;

  protected readonly tasks = inject(TaskService).listByProject(() => ({
    projectId: this.projectId(),
    page: this.page(),
    pageSize: this.pageSize(),
  }));
  /** The latest loaded page; kept while the next one loads, so the section does not flash. */
  protected readonly result = linkedSignal<PagedResult<TaskListItem> | undefined, PagedResult<TaskListItem> | undefined>({
    source: () => (this.tasks.hasValue() ? this.tasks.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });
  protected readonly errorMessage = computed(() => {
    const error = this.tasks.error();
    return error ? apiErrorMessage(error, "The project's tasks could not be loaded.") : null;
  });

  protected readonly isPhone = toSignal(inject(BreakpointObserver).observe(phoneQuery).pipe(map((r) => r.matches)), {
    initialValue: inject(BreakpointObserver).isMatched(phoneQuery),
  });
  protected readonly currentUserId = computed(() => this.auth.user()?.id ?? '');
  protected readonly today = utcToday();
  protected readonly skeletonRows = [1, 2, 3];

  constructor() {
    // After a delete empties the last page, step back to the new last page.
    effect(() => {
      const result = this.result();
      if (result && result.items.length === 0 && result.totalCount > 0 && this.page() > result.totalPages) {
        this.page.set(result.totalPages);
      }
    });
  }

  protected overdue(task: TaskListItem): boolean {
    return isOverdue(task.dueDate, task.status, this.today);
  }

  protected caption(task: TaskListItem): string {
    return task.dueDate ? dueCaption(task.dueDate, task.status, this.today) : '';
  }

  protected pageChange(event: PageEvent): void {
    const sizeChanged = event.pageSize !== this.pageSize();
    this.pageSize.set(event.pageSize);
    this.page.set(sizeChanged ? 1 : event.pageIndex + 1);
  }

  protected create(): void {
    this.dialogs
      .open({ projectId: this.projectId(), fixedProject: { name: this.projectName() } })
      .subscribe((result) => this.afterDialog(result));
  }

  protected open(task: TaskListItem): void {
    this.dialogs.open({ projectId: task.projectId, taskId: task.id }).subscribe((result) => this.afterDialog(result));
  }

  protected retry(): void {
    this.tasks.reload();
  }

  private afterDialog(result: string | undefined): void {
    if (result) {
      this.tasks.reload();
      this.changed.emit();
    }
  }
}
