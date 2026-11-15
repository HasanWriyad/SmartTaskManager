import { BreakpointObserver } from '@angular/cdk/layout';
import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRouteSnapshot, NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { Role } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { Avatar } from '../../shared/avatar/avatar';
import { Logo } from '../../shared/logo/logo';
import { ThemeToggle } from '../../shared/theme-toggle/theme-toggle';

interface NavItem {
  path: string;
  label: string;
  icon: string;
}

/**
 * Sidebar layout per spec §6:
 * - wide (>= 1200px): expanded 256px sidebar, collapsible to the icon rail (choice saved);
 * - rail (720-1199px): 72px icon rail;
 * - drawer (< 720px): 280px slide-out drawer over a scrim, opened from the header's menu button.
 */
type Layout = 'wide' | 'rail' | 'drawer';

const wideQuery = '(min-width: 1200px)';
const railQuery = '(min-width: 720px)';
const collapsedKey = 'stm-sidebar-collapsed';

const roleLabels: Record<Role, string> = {
  Admin: 'Admin',
  ProjectManager: 'Project Manager',
  TeamMember: 'Team Member',
};

/** Layout for signed-in pages: left sidebar navigation and a header with the page title. */
@Component({
  selector: 'app-shell',
  imports: [
    NgTemplateOutlet,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    Avatar,
    Logo,
    ThemeToggle,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly breakpoints = inject(BreakpointObserver);

  protected readonly user = this.auth.user;
  protected readonly isAdmin = computed(() => this.auth.role() === 'Admin');
  protected readonly roleLabel = computed(() => {
    const role = this.auth.role();
    return role ? roleLabels[role] : '';
  });

  protected readonly mainNav: NavItem[] = [
    { path: '/dashboard', label: 'Dashboard', icon: 'grid_view' },
    { path: '/projects', label: 'Projects', icon: 'folder' },
    { path: '/tasks', label: 'Tasks', icon: 'check_box' },
  ];
  protected readonly adminNav: NavItem[] = [{ path: '/users', label: 'Users', icon: 'group' }];

  protected readonly layout = toSignal(
    this.breakpoints.observe([wideQuery, railQuery]).pipe(map(() => this.currentLayout())),
    { initialValue: this.currentLayout() },
  );

  /** The user's collapse choice for the wide layout. */
  protected readonly collapsed = signal(readCollapsed());
  protected readonly drawerOpen = signal(false);

  protected readonly isDrawer = computed(() => this.layout() === 'drawer');
  /** Icon rail: labels hidden, icons centred. */
  protected readonly compact = computed(() => this.layout() === 'rail' || (this.layout() === 'wide' && this.collapsed()));
  protected readonly opened = computed(() => !this.isDrawer() || this.drawerOpen());

  /** Page title from the active route's data.heading. */
  protected readonly heading = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => this.currentHeading()),
    ),
    { initialValue: this.currentHeading() },
  );

  constructor() {
    // Navigating from the drawer closes it.
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.drawerOpen.set(false));

    // Leaving the phone layout resets the drawer, so it is closed when the window shrinks again.
    effect(() => {
      if (!this.isDrawer()) {
        this.drawerOpen.set(false);
      }
    });
  }

  protected toggleCollapsed(): void {
    const next = !this.collapsed();
    this.collapsed.set(next);
    try {
      localStorage.setItem(collapsedKey, String(next));
    } catch {
      // Storage blocked: the choice lasts for this page only.
    }
  }

  protected logout(): void {
    this.auth.logout().subscribe();
  }

  private currentLayout(): Layout {
    if (this.breakpoints.isMatched(wideQuery)) {
      return 'wide';
    }
    return this.breakpoints.isMatched(railQuery) ? 'rail' : 'drawer';
  }

  private currentHeading(): string {
    let route: ActivatedRouteSnapshot | null = this.router.routerState.snapshot.root;
    let heading = '';
    while (route) {
      heading = (route.data['heading'] as string | undefined) ?? heading;
      route = route.firstChild;
    }
    return heading;
  }
}

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(collapsedKey) === 'true';
  } catch {
    return false;
  }
}
