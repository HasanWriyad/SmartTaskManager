import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Logo } from '../../../shared/logo/logo';
import { LogoMark } from '../../../shared/logo/logo-mark';
import { ThemeToggle } from '../../../shared/theme-toggle/theme-toggle';

/**
 * Two-column layout for the sign-in and registration pages (spec §7.1): the form panel on the
 * left, a decorative brand panel on the right (hidden below 900px).
 */
@Component({
  selector: 'app-auth-layout',
  imports: [Logo, LogoMark, ThemeToggle],
  templateUrl: './auth-layout.html',
  styleUrl: './auth-layout.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthLayout {
  protected readonly year = new Date().getFullYear();
}
