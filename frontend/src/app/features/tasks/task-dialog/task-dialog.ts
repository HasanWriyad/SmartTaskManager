import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, Injectable, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideAppDateAdapter } from '../../../shared/dates/app-date-adapter';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Observable, filter, firstValueFrom, map, startWith } from 'rxjs';
import { AiService } from '../../../core/ai/ai.service';
import { ApiError, apiErrorMessage, apiErrorStatus } from '../../../core/api/api-error';
import { AuthService } from '../../../core/auth/auth.service';
import { applyServerErrors } from '../../../core/forms/server-errors';
import { NotificationService } from '../../../core/notifications/notification.service';
import { toIsoDate } from '../../../core/tasks/task-due';
import { TaskPriority, TaskStatus, taskPriorityLabels, taskStatusLabels } from '../../../core/tasks/task.models';
import { ConfirmService } from '../../../shared/confirm-dialog/confirm-dialog';
import { defaultProjectListState } from '../../projects/project.models';
import { ProjectService } from '../../projects/project.service';
import {
  TaskDetails,
  aiInputMaxLength,
  taskDescriptionMaxLength,
  taskTitleMaxLength,
} from '../task.models';
import { TaskService } from '../task.service';

/** Create (optionally in a given project) or open an existing task. */
export type TaskDialogData = { projectId?: string; taskId?: undefined } | { projectId: string; taskId: string };
/** 'saved' when anything was saved (even if a later step failed and the user then cancelled). */
export type TaskDialogResult = 'saved' | 'deleted' | undefined;

type Step = { label: string; run: () => Observable<TaskDetails> };

const priorities: TaskPriority[] = ['Critical', 'High', 'Medium', 'Low'];
const statuses: TaskStatus[] = ['ToDo', 'InProgress', 'Completed', 'Cancelled'];

/**
 * Task form dialog (spec §7.4). Creating: project (a project the user manages), title, description,
 * assignee (the project's members), priority and due date, in one POST. Editing: the task's own
 * canManage and canUpdateStatus decide what is editable, and only what changed is sent, through the
 * existing endpoints (fields, assignee and status are separate calls). "Improve with AI" (Admin and
 * ProjectManager) suggests a description; it is applied only when the user chooses Apply.
 */
@Component({
  selector: 'app-task-dialog',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
  ],
  providers: [provideAppDateAdapter()],
  templateUrl: './task-dialog.html',
  styleUrl: './task-dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskDialog {
  private readonly tasks = inject(TaskService);
  private readonly ai = inject(AiService);
  private readonly auth = inject(AuthService);
  private readonly notifications = inject(NotificationService);
  private readonly confirmService = inject(ConfirmService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialogRef = inject<MatDialogRef<TaskDialog, TaskDialogResult>>(MatDialogRef);
  private readonly data = inject<TaskDialogData>(MAT_DIALOG_DATA);
  private readonly projectsApi = inject(ProjectService);

  protected readonly isCreate = !this.data.taskId;
  protected readonly titleMax = taskTitleMaxLength;
  protected readonly descriptionMax = taskDescriptionMaxLength;
  protected readonly priorities = priorities;
  protected readonly priorityLabels = taskPriorityLabels;
  protected readonly statusLabels = taskStatusLabels;

  /** The task as last loaded or saved (edit only): the baseline for "what changed". */
  protected readonly task = signal<TaskDetails | null>(null);
  protected readonly loadError = signal<{ notFound: boolean; message: string } | null>(null);
  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  /** Something was saved during this dialog's life (so the opener refreshes even after a later failure). */
  private savedSomething = false;

  protected readonly form = inject(NonNullableFormBuilder).group({
    projectId: [this.data.projectId ?? '', Validators.required],
    title: ['', [Validators.required, Validators.maxLength(taskTitleMaxLength), notBlank]],
    description: ['', Validators.maxLength(taskDescriptionMaxLength)],
    assigneeId: [''],
    status: ['ToDo' as TaskStatus],
    priority: ['Medium' as TaskPriority],
    dueDate: [null as Date | null],
  });

  // ---------- Permissions (edit: from the task; create: the user can manage the chosen project)
  protected readonly canEditFields = computed(() => this.isCreate || !!this.task()?.canManage);
  protected readonly canEditStatus = computed(() => !!this.task()?.canUpdateStatus && this.statusOptions().length > 1);
  protected readonly canSave = computed(() => this.canEditFields() || this.canEditStatus());
  protected readonly canDelete = computed(() => !this.isCreate && !!this.task()?.canManage);
  /** AI is for Admin and ProjectManager, and only where they may edit the description. */
  protected readonly canUseAi = computed(() => this.auth.hasAnyRole(['Admin', 'ProjectManager']) && this.canEditFields());
  protected readonly heading = computed(() => (this.isCreate ? 'New task' : this.task()?.canManage ? 'Edit task' : 'Task'));

  /**
   * Leaving Cancelled means reopening to To do, which only a manager may do; otherwise any status.
   * A single option means the status cannot change.
   */
  protected readonly statusOptions = computed<TaskStatus[]>(() => {
    const task = this.task();
    if (!task) {
      return [];
    }
    if (task.status === 'Cancelled') {
      return task.canManage ? ['Cancelled', 'ToDo'] : ['Cancelled'];
    }
    return statuses;
  });

  // ---------- Projects (create) and members (assignee options)
  /** Projects the user can manage: the only ones a task can be created in. */
  private readonly projectList = this.isCreate
    ? this.projectsApi.list(() => ({ ...defaultProjectListState, pageSize: 100 }))
    : null;
  protected readonly manageableProjects = computed(() => {
    const list = this.projectList;
    return list?.hasValue() ? list.value().items.filter((p) => p.canManage) : [];
  });
  protected readonly projectsLoaded = computed(() => !this.projectList || this.projectList.hasValue() || !!this.projectList.error());

  private readonly projectIdValue = toSignal(
    this.form.controls.projectId.valueChanges.pipe(startWith(this.form.controls.projectId.value)),
    { initialValue: this.form.controls.projectId.value },
  );
  protected readonly selectedProjectId = computed(() => (this.isCreate ? this.projectIdValue() : this.task()?.projectId) || undefined);
  private readonly project = this.projectsApi.details(() => this.selectedProjectId());
  protected readonly members = computed(() => {
    const project = this.project.hasValue() ? this.project.value() : undefined;
    return project ? [...project.members].sort((a, b) => a.fullName.localeCompare(b.fullName)) : [];
  });

  // ---------- AI suggestion
  protected readonly aiLoading = signal(false);
  protected readonly suggestion = signal<string | null>(null);
  protected readonly aiError = signal<string | null>(null);
  protected readonly aiApplied = signal(false);
  private readonly descriptionValue = toSignal(this.form.controls.description.valueChanges, { initialValue: '' });
  protected readonly descriptionEmpty = computed(() => !this.descriptionValue().trim());

  constructor() {
    if (this.isCreate) {
      this.form.controls.status.disable();
      // A preset project the user cannot manage is dropped; a single manageable project is preselected.
      effect(() => {
        if (!this.projectList?.hasValue()) {
          return;
        }
        const ids = this.manageableProjects().map((p) => p.id);
        const control = this.form.controls.projectId;
        if (control.value && !ids.includes(control.value)) {
          control.setValue('');
        }
        if (!control.value && ids.length === 1) {
          control.setValue(ids[0]);
        }
      });
    } else {
      this.form.disable();
      this.load();
    }

    // Changing the project on create clears the assignee (members differ per project).
    this.form.controls.projectId.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.form.controls.assigneeId.setValue(''));

    // Escape and the backdrop go through close(), so a partial save still refreshes the opener.
    this.dialogRef.disableClose = true;
    this.dialogRef
      .backdropClick()
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.close());
    this.dialogRef
      .keydownEvents()
      .pipe(
        filter((event) => event.key === 'Escape'),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.close());
  }

  /** Closing with Cancel or Escape after a partial save still tells the opener to refresh. */
  protected close(): void {
    if (!this.saving()) {
      this.dialogRef.close(this.savedSomething ? 'saved' : undefined);
    }
  }

  protected retryLoad(): void {
    this.load();
  }

  private load(): void {
    const { projectId, taskId } = this.data;
    this.loadError.set(null);
    this.tasks
      .get(projectId!, taskId!)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (task) => this.setBaseline(task),
        error: (error: unknown) => {
          const status = apiErrorStatus(error);
          this.loadError.set({
            notFound: status === 404 || status === 400,
            message: apiErrorMessage(error, 'The task could not be loaded.'),
          });
        },
      });
  }

  /** Fills the form from the task and enables only what the user may change. */
  private setBaseline(task: TaskDetails): void {
    this.task.set(task);
    this.form.reset({
      projectId: task.projectId,
      title: task.title,
      description: task.description ?? '',
      assigneeId: task.assigneeId ?? '',
      status: task.status,
      priority: task.priority,
      dueDate: task.dueDate ? fromIsoDate(task.dueDate) : null,
    });
    this.form.disable({ emitEvent: false });
    if (task.canManage) {
      for (const name of ['title', 'description', 'assigneeId', 'priority', 'dueDate'] as const) {
        this.form.controls[name].enable({ emitEvent: false });
      }
    }
    if (this.canEditStatus()) {
      this.form.controls.status.enable({ emitEvent: false });
    }
  }

  // ---------- Save
  protected async submit(): Promise<void> {
    if (this.saving() || !this.canSave()) {
      return;
    }
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }
    this.errorMessage.set(null);

    if (this.isCreate) {
      await this.run([{ label: 'create', run: () => this.createRequest() }], 'Task created.');
      return;
    }

    const steps = this.changedSteps();
    if (steps.length === 0) {
      this.close();
      return;
    }
    await this.run(steps, steps.length === 1 && steps[0].label === 'status' ? 'Status updated.' : 'Task updated.');
  }

  private createRequest(): Observable<TaskDetails> {
    const v = this.form.getRawValue();
    return this.tasks.create(v.projectId, {
      title: v.title.trim(),
      description: v.description.trim() || null,
      priority: v.priority,
      dueDate: v.dueDate ? toIsoDate(v.dueDate) : null,
      assigneeId: v.assigneeId || null,
    });
  }

  /** Only what differs from the baseline, as the endpoints that change it. */
  private changedSteps(): Step[] {
    const task = this.task()!;
    const v = this.form.getRawValue();
    const steps: Step[] = [];

    const fields = {
      title: v.title.trim(),
      description: v.description.trim() || null,
      priority: v.priority,
      dueDate: v.dueDate ? toIsoDate(v.dueDate) : null,
    };
    const fieldsChanged =
      fields.title !== task.title ||
      fields.description !== (task.description ?? null) ||
      fields.priority !== task.priority ||
      fields.dueDate !== task.dueDate;
    if (task.canManage && fieldsChanged) {
      steps.push({ label: 'fields', run: () => this.tasks.update(task.projectId, task.id, fields) });
    }

    const assigneeId = v.assigneeId || null;
    if (task.canManage && assigneeId !== task.assigneeId) {
      steps.push({ label: 'assignee', run: () => this.tasks.assign(task.projectId, task.id, assigneeId) });
    }

    if (task.canUpdateStatus && v.status !== task.status) {
      steps.push({ label: 'status', run: () => this.tasks.changeStatus(task.projectId, task.id, v.status) });
    }
    return steps;
  }

  /** Runs the steps in order; each success becomes the new baseline, so a retry sends only the rest. */
  private async run(steps: Step[], successMessage: string): Promise<void> {
    this.saving.set(true);
    try {
      for (const step of steps) {
        const saved = await firstValueFrom(step.run());
        this.savedSomething = true;
        if (!this.isCreate) {
          this.task.set(saved);
        }
      }
      this.notifications.success(successMessage);
      this.dialogRef.close('saved');
    } catch (error) {
      this.saving.set(false);
      if (!(error instanceof ApiError && applyServerErrors(this.form, error))) {
        this.errorMessage.set(apiErrorMessage(error, 'The task could not be saved.'));
      }
    }
  }

  // ---------- Delete
  protected delete(): void {
    const task = this.task();
    if (!task || this.saving()) {
      return;
    }
    this.confirmService
      .confirm({
        title: 'Delete task?',
        message: `"${task.title}" will be deleted.`,
        detail: 'Deleted tasks cannot be restored from the app.',
        confirmLabel: 'Delete task',
        destructive: true,
      })
      .pipe(filter(Boolean), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.saving.set(true);
        this.tasks.delete(task.projectId, task.id).subscribe({
          next: () => {
            this.notifications.success('Task deleted.');
            this.dialogRef.close('deleted');
          },
          error: (error: unknown) => {
            this.saving.set(false);
            this.errorMessage.set(apiErrorMessage(error, 'The task could not be deleted.'));
          },
        });
      });
  }

  // ---------- AI
  protected improve(): void {
    const description = this.form.controls.description.value.trim();
    if (!description || this.aiLoading()) {
      return;
    }
    this.aiError.set(null);
    this.aiApplied.set(false);
    this.suggestion.set(null);
    if (description.length > aiInputMaxLength) {
      this.aiError.set(`Improve with AI works on descriptions up to ${aiInputMaxLength.toLocaleString()} characters.`);
      return;
    }
    this.aiLoading.set(true);
    this.ai
      .improveDescription(description, this.form.controls.title.value.trim() || null)
      .pipe(
        map((response) => response.improvedDescription),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (text) => {
          this.aiLoading.set(false);
          this.suggestion.set(text);
        },
        error: (error: unknown) => {
          this.aiLoading.set(false);
          // 429 (rate limit), 502, 503 and 504 carry a user-safe message from the API.
          this.aiError.set(apiErrorMessage(error, 'The suggestion could not be generated. Try again.'));
        },
      });
  }

  protected applySuggestion(): void {
    const text = this.suggestion();
    if (!text) {
      return;
    }
    const control = this.form.controls.description;
    control.setValue(text.slice(0, taskDescriptionMaxLength));
    control.markAsDirty();
    this.suggestion.set(null);
    this.aiApplied.set(true);
  }

  protected discardSuggestion(): void {
    this.suggestion.set(null);
  }
}

function notBlank(control: { value: string }) {
  return control.value && !control.value.trim() ? { required: true } : null;
}

function fromIsoDate(value: string): Date {
  const [y, m, d] = value.split('-').map(Number);
  return new Date(y, m - 1, d);
}

@Injectable({ providedIn: 'root' })
export class TaskDialogService {
  private readonly dialog = inject(MatDialog);

  /** New task (optionally preselecting a project), or an existing task by project and id. */
  open(data: TaskDialogData = {}): Observable<TaskDialogResult> {
    const ref = this.dialog.open<TaskDialog, TaskDialogData, TaskDialogResult>(TaskDialog, {
      data,
      width: '600px',
      maxWidth: '100vw',
      panelClass: 'app-form-dialog',
      autoFocus: data.taskId ? 'dialog' : 'first-tabbable',
      ariaLabelledBy: 'task-dialog-title',
    });
    return ref.afterClosed();
  }
}
