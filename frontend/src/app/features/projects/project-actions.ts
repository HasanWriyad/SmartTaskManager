import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { EMPTY, Observable, catchError, filter, map, switchMap } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { NotificationService } from '../../core/notifications/notification.service';
import { ConfirmService } from '../../shared/confirm-dialog/confirm-dialog';
import { ProjectDialogService } from './project-dialog/project-dialog';
import { ProjectDetails } from './project.models';
import { ProjectService } from './project.service';

type ProjectRef = { id: string; name: string; description: string | null; taskCount: number };

/** Create, edit and delete flows shared by the project list, the details page and the dashboard. */
@Injectable({ providedIn: 'root' })
export class ProjectActions {
  private readonly projects = inject(ProjectService);
  private readonly dialogs = inject(ProjectDialogService);
  private readonly confirmService = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  /** Opens the new-project dialog; a created project opens on its details page, ready for members. */
  create(): void {
    this.dialogs
      .open()
      .pipe(filter((project) => !!project))
      .subscribe((project) => void this.router.navigate(['/projects', project.id]));
  }

  /** Emits the saved project; completes without emitting when cancelled. */
  edit(project: ProjectRef): Observable<ProjectDetails> {
    return this.dialogs.open(project).pipe(filter((saved): saved is ProjectDetails => !!saved));
  }

  /** Asks first (the tasks go too), then deletes. Emits once deleted; errors are shown here. */
  delete(project: ProjectRef): Observable<void> {
    const count = project.taskCount;
    const message =
      count === 0
        ? `"${project.name}" will be deleted. Deleting a project also deletes its tasks; this one has none yet.`
        : `"${project.name}" will be deleted, together with ${count === 1 ? 'its 1 task' : `all ${count} of its tasks`}.`;
    return this.confirmService
      .confirm({
        title: 'Delete project?',
        message,
        detail: 'Deleted projects and tasks cannot be restored from the app.',
        confirmLabel: 'Delete project',
        destructive: true,
      })
      .pipe(
        filter(Boolean),
        switchMap(() =>
          this.projects.delete(project.id).pipe(
            catchError((error: unknown) => {
              this.notifications.error(apiErrorMessage(error, 'The project could not be deleted.'));
              return EMPTY;
            }),
          ),
        ),
        map(() => {
          this.notifications.success('Project deleted.');
        }),
      );
  }
}
