import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Avatar } from '../../shared/avatar/avatar';
import { PriorityBadge } from '../../shared/badges/priority-badge';
import { StatusBadge } from '../../shared/badges/status-badge';
import { UpcomingTask } from './dashboard.models';
import { relativeDue } from './dashboard.view';

/**
 * "Upcoming due" table: Task (title + project), Assignee, Due (date + relative), Priority,
 * Status. Below 960px Assignee and Status are hidden; below 720px Priority too.
 */
@Component({
  selector: 'app-upcoming-list',
  imports: [DatePipe, Avatar, PriorityBadge, StatusBadge],
  template: `
    @if (tasks().length === 0) {
      <p class="empty">Nothing is due in the next {{ windowDays() }} days.</p>
    } @else {
      <table class="table">
        <thead>
          <tr>
            <th scope="col">Task</th>
            <th scope="col" class="wide-only">Assignee</th>
            <th scope="col">Due</th>
            <th scope="col" class="tablet-up">Priority</th>
            <th scope="col" class="wide-only">Status</th>
          </tr>
        </thead>
        <tbody>
          @for (task of tasks(); track task.id) {
            <tr class="row" (click)="openTask.emit(task)">
              <td>
                <button type="button" class="title" (click)="openTask.emit(task); $event.stopPropagation()">{{ task.title }}</button>
                <span class="meta">{{ task.projectName }}</span>
              </td>
              <td class="wide-only">
                @if (task.assigneeName; as name) {
                  <span class="assignee">
                    <app-avatar [name]="name" [size]="28" [self]="task.assigneeId === currentUserId()" />
                    <span class="assignee-name">{{ name }}{{ task.assigneeId === currentUserId() ? ' (you)' : '' }}</span>
                  </span>
                } @else {
                  <span class="meta">Unassigned</span>
                }
              </td>
              <td class="due">
                <!-- DatePipe reads a date-only string as local midnight, so no timezone argument. -->
                <span class="tabular">{{ task.dueDate | date: 'EEE, d MMM' }}</span>
                <span class="meta">{{ relative(task.dueDate) }}</span>
              </td>
              <td class="tablet-up"><app-priority-badge [priority]="task.priority" /></td>
              <td class="wide-only"><app-status-badge [status]="task.status" /></td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styleUrl: './upcoming-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UpcomingList {
  readonly tasks = input.required<UpcomingTask[]>();
  readonly asOf = input.required<string>();
  readonly windowDays = input(7);
  readonly currentUserId = input<string | null>(null);
  /** A row (or its title button, for keyboard users) was chosen: open the task dialog. */
  readonly openTask = output<UpcomingTask>();

  protected relative(dueDate: string): string {
    return relativeDue(dueDate, this.asOf());
  }
}
