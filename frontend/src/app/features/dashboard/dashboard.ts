import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ProjectActions } from '../projects/project-actions';
import { TaskDialogService } from '../tasks/task-dialog/task-dialog';
import { UpcomingTask } from './dashboard.models';
import { DashboardService } from './dashboard.service';
import { priorityRows, statusSlices, summaryCards } from './dashboard.view';
import { PriorityChart } from './priority-chart';
import { StatusChart } from './status-chart';
import { UpcomingList } from './upcoming-list';

/** Rows shown in "Upcoming due" (the API returns up to 10). */
const upcomingRows = 5;

/** Dashboard (spec §7.2): summary cards, status and priority charts, and tasks due in the next 7 days. */
@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, MatButtonModule, MatIconModule, PriorityChart, StatusChart, UpcomingList],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Dashboard {
  private readonly auth = inject(AuthService);
  private readonly projectActions = inject(ProjectActions);
  private readonly taskDialogs = inject(TaskDialogService);

  protected readonly dashboard = inject(DashboardService).load();

  protected readonly isAdmin = computed(() => this.auth.role() === 'Admin');
  protected readonly currentUserId = computed(() => this.auth.user()?.id ?? null);

  protected readonly data = computed(() => (this.dashboard.hasValue() ? this.dashboard.value() : undefined));
  protected readonly errorMessage = computed(() => {
    const error = this.dashboard.error();
    return error ? DashboardService.errorMessage(error) : null;
  });

  protected readonly cards = computed(() => {
    const d = this.data();
    return d ? summaryCards(d, this.isAdmin()) : [];
  });
  protected readonly slices = computed(() => {
    const d = this.data();
    return d ? statusSlices(d) : [];
  });
  protected readonly priorities = computed(() => {
    const d = this.data();
    return d ? priorityRows(d) : [];
  });
  protected readonly upcoming = computed(() => this.data()?.upcomingTasks.slice(0, upcomingRows) ?? []);
  protected readonly scopeLabel = computed(() => {
    const total = this.data()?.totalTasks ?? 0;
    return `${this.isAdmin() ? 'All projects' : 'Your projects'} · ${total} ${total === 1 ? 'task' : 'tasks'}`;
  });

  protected readonly skeletonCards = [1, 2, 3, 4, 5, 6];
  protected readonly skeletonRows = [1, 2, 3, 4, 5];

  /** Admin and ProjectManager can create projects (the API enforces it). */
  protected readonly canCreateProject = computed(() => this.auth.hasAnyRole(['Admin', 'ProjectManager']));

  /** Opens the new-project dialog; the created project opens on its details page. */
  protected createProject(): void {
    this.projectActions.create();
  }

  /** Upcoming rows open the task dialog; the numbers refresh after a save or delete. */
  protected openTask(task: UpcomingTask): void {
    this.taskDialogs.open({ projectId: task.projectId, taskId: task.id }).subscribe((result) => {
      if (result) {
        this.dashboard.reload();
      }
    });
  }

  protected retry(): void {
    this.dashboard.reload();
  }
}
