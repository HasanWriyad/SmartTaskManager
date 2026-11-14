import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Role } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  roles?: Role[];
}

const roleLabels: Record<Role, string> = {
  Admin: 'Admin',
  ProjectManager: 'Project Manager',
  TeamMember: 'Team Member',
};

/** Layout for signed-in pages: toolbar with navigation and the user menu. */
@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatDividerModule,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell {
  private readonly auth = inject(AuthService);

  protected readonly user = this.auth.user;
  protected readonly roleLabel = computed(() => {
    const role = this.auth.role();
    return role ? roleLabels[role] : '';
  });

  private readonly allNavItems: NavItem[] = [
    { path: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
    { path: '/users', label: 'Users', icon: 'group', roles: ['Admin'] },
  ];

  protected readonly navItems = computed(() =>
    this.allNavItems.filter((item) => !item.roles || this.auth.hasAnyRole(item.roles)),
  );

  protected logout(): void {
    this.auth.logout().subscribe();
  }
}
