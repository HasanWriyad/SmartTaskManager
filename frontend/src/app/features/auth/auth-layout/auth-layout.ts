import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TaskStatus } from '../../../core/tasks/task.models';
import { StatusBadge } from '../../../shared/badges/status-badge';
import { Logo } from '../../../shared/logo/logo';
import { ThemeToggle } from '../../../shared/theme-toggle/theme-toggle';

interface SampleTask {
  title: string;
  meta: string;
  status: TaskStatus;
}

/**
 * Two-column layout for the sign-in and registration pages (spec §7.1): the form panel on the
 * left, a product panel with sample task rows on the right (hidden below 900px).
 */
@Component({
  selector: 'app-auth-layout',
  imports: [Logo, StatusBadge, ThemeToggle],
  templateUrl: './auth-layout.html',
  styleUrl: './auth-layout.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthLayout {
  protected readonly year = new Date().getFullYear();

  /** Illustration only; hidden from assistive technology. */
  protected readonly samples: SampleTask[] = [
    { title: 'Implement JWT refresh tokens', meta: 'Customer Portal · Due tomorrow', status: 'InProgress' },
    { title: 'Design project settings page', meta: 'Website Redesign · Due in 2 days', status: 'ToDo' },
    { title: 'Migrate invoices table', meta: 'Billing Service · Done', status: 'Completed' },
  ];
}
