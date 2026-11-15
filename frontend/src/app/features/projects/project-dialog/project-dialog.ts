import { ChangeDetectionStrategy, Component, Injectable, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Observable } from 'rxjs';
import { ApiError, apiErrorMessage } from '../../../core/api/api-error';
import { applyServerErrors } from '../../../core/forms/server-errors';
import { NotificationService } from '../../../core/notifications/notification.service';
import { ProjectDetails, ProjectRequest, projectDescriptionMaxLength, projectNameMaxLength } from '../project.models';
import { ProjectService } from '../project.service';

/** The project being edited; absent when creating. */
export interface ProjectDialogData {
  project?: { id: string; name: string; description: string | null };
}

/**
 * Create or edit a project (name and description). Saves through POST or PUT /api/projects and
 * closes with the saved project; field errors from the API (e.g. a duplicate name) show inline.
 */
@Component({
  selector: 'app-project-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule],
  templateUrl: './project-dialog.html',
  styleUrl: './project-dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectDialog {
  private readonly projects = inject(ProjectService);
  private readonly notifications = inject(NotificationService);
  private readonly dialogRef = inject<MatDialogRef<ProjectDialog, ProjectDetails>>(MatDialogRef);
  private readonly data = inject<ProjectDialogData>(MAT_DIALOG_DATA);

  protected readonly editing = this.data.project;
  protected readonly nameMax = projectNameMaxLength;
  protected readonly descriptionMax = projectDescriptionMaxLength;

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.editing?.name ?? '', [Validators.required, Validators.maxLength(projectNameMaxLength), notBlank]],
    description: [this.editing?.description ?? '', [Validators.maxLength(projectDescriptionMaxLength)]],
  });

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected submit(): void {
    if (this.saving()) {
      return;
    }
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }
    // Editing without changes: nothing to send.
    if (this.editing && this.form.pristine) {
      this.dialogRef.close();
      return;
    }

    const value = this.form.getRawValue();
    const request: ProjectRequest = { name: value.name.trim(), description: value.description.trim() || null };
    const save$ = this.editing ? this.projects.update(this.editing.id, request) : this.projects.create(request);

    this.saving.set(true);
    this.errorMessage.set(null);
    this.dialogRef.disableClose = true;
    save$.subscribe({
      next: (project) => {
        this.notifications.success(this.editing ? 'Project updated.' : 'Project created.');
        this.dialogRef.close(project);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.dialogRef.disableClose = false;
        // A duplicate name (409) belongs to the name field; other field errors map by name.
        if (error instanceof ApiError && error.status === 409) {
          this.form.controls.name.setErrors({ server: error.message });
          this.form.controls.name.markAsTouched();
          return;
        }
        if (error instanceof ApiError && applyServerErrors(this.form, error)) {
          return;
        }
        this.errorMessage.set(apiErrorMessage(error, 'The project could not be saved.'));
      },
    });
  }
}

function notBlank(control: { value: string }) {
  return control.value && !control.value.trim() ? { required: true } : null;
}

@Injectable({ providedIn: 'root' })
export class ProjectDialogService {
  private readonly dialog = inject(MatDialog);

  /** Opens the dialog; emits the saved project, or undefined when cancelled. */
  open(project?: ProjectDialogData['project']): Observable<ProjectDetails | undefined> {
    return this.dialog
      .open<ProjectDialog, ProjectDialogData, ProjectDetails>(ProjectDialog, {
        data: { project },
        width: '560px',
        maxWidth: '100vw',
        panelClass: 'app-form-dialog',
        autoFocus: 'first-tabbable',
        ariaLabelledBy: 'project-dialog-title',
      })
      .afterClosed();
  }
}
