import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth.guards';
import { Role } from './core/auth/auth.models';

// Every page is lazy-loaded. Signed-in pages are children of the Shell layout behind authGuard;
// roleGuard reads the allowed roles from route data.
export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Sign in · Smart Task Manager',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    title: 'Create account · Smart Task Manager',
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
  },
  {
    path: '',
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    loadComponent: () => import('./layout/shell/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard · Smart Task Manager',
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'users',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] satisfies Role[] },
        title: 'Users · Smart Task Manager',
        loadComponent: () => import('./features/users/users').then((m) => m.Users),
      },
      {
        path: 'forbidden',
        title: 'Access denied · Smart Task Manager',
        loadComponent: () => import('./features/errors/forbidden').then((m) => m.Forbidden),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
