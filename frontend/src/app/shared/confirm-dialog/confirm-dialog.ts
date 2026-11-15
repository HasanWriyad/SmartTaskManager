import { ChangeDetectionStrategy, Component, Injectable, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';

export interface ConfirmOptions {
  title: string;
  message: string;
  /** Optional second paragraph, e.g. what else the action affects. */
  detail?: string;
  confirmLabel: string;
  /** Destructive actions get the red confirm button. */
  destructive?: boolean;
}

/** A small yes/no dialog. Closes with true when confirmed. */
@Component({
  selector: 'app-confirm-dialog',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p>{{ data.message }}</p>
      @if (data.detail) {
        <p class="detail">{{ data.detail }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" [mat-dialog-close]="false">Cancel</button>
      <button mat-flat-button type="button" [class.danger]="data.destructive" [mat-dialog-close]="true">
        {{ data.confirmLabel }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    p {
      margin: 0;
      color: var(--text);
    }
    .detail {
      margin-top: 12px;
      color: var(--text-muted);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmOptions>(MAT_DIALOG_DATA);
}

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(MatDialog);

  /** Emits true when the user confirms, false otherwise. */
  confirm(options: ConfirmOptions): Observable<boolean> {
    return this.dialog
      .open<ConfirmDialog, ConfirmOptions, boolean>(ConfirmDialog, {
        data: options,
        width: '440px',
        maxWidth: 'calc(100vw - 32px)',
        autoFocus: 'dialog',
        role: 'alertdialog',
      })
      .afterClosed()
      .pipe(map((confirmed) => confirmed === true));
  }
}
