import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },

  { path: 'register', loadChildren: () => import('./features/auth/register/register.routes').then(m => m.REGISTER_ROUTES) },
  { path: 'login',    loadChildren: () => import('./features/auth/login/login.routes').then(m => m.LOGIN_ROUTES) },

  { path: 'lobby',  canActivate: [authGuard], loadComponent: () => import('./features/lobby/lobby.component').then(m => m.LobbyComponent) },
  { path: 'rooms/:code', canActivate: [authGuard], loadComponent: () => import('./features/room/room.component').then(m => m.RoomComponent) },

  { path: '**', redirectTo: 'login' },
];
