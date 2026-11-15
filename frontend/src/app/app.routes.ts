import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth.guards';
import { Role } from './core/auth/auth.models';

// Every page is lazy-loaded. Signed-in pages are children of the Shell layout behind authGuard;
// roleGuard reads the allowed roles from route data. data.heading is the title in the shell header.
export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Sign in · SmartTask',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    title: 'Create account · SmartTask',
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
        title: 'Dashboard · SmartTask',
        data: { heading: 'Dashboard' },
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'projects',
        title: 'Projects · SmartTask',
        data: { heading: 'Projects' },
        loadComponent: () => import('./features/projects/projects').then((m) => m.Projects),
      },
      {
        path: 'tasks',
        title: 'Tasks · SmartTask',
        data: { heading: 'Tasks' },
        loadComponent: () => import('./features/tasks/tasks').then((m) => m.Tasks),
      },
      {
        path: 'users',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] satisfies Role[], heading: 'Users' },
        title: 'Users · SmartTask',
        loadComponent: () => import('./features/users/users').then((m) => m.Users),
      },
      {
        path: 'forbidden',
        title: 'Access denied · SmartTask',
        data: { heading: 'Access denied' },
        loadComponent: () => import('./features/errors/forbidden').then((m) => m.Forbidden),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
