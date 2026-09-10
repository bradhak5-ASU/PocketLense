import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { AuthService } from './services/auth.service';
import { Login } from './pages/login/login';
import { Shell } from './pages/shell/shell';

const signedIn: CanActivateFn = () =>
    inject(AuthService).loggedIn || inject(Router).createUrlTree(['/login']);

export const routes: Routes = [
    { path: 'login', component: Login },
    { path: 'register', component: Login },
    {
        path: '',
        component: Shell,
        canActivate: [signedIn],
        canActivateChild: [signedIn],
        children: [
            {
                path: 'dashboard',
                loadComponent: () =>
                    import('./pages/dashboard/dashboard').then((m) => m.DashboardPage),
            },
            {
                path: 'transactions',
                loadComponent: () =>
                    import('./pages/transactions/transactions').then((m) => m.TransactionsPage),
            },
            {
                path: 'import',
                loadComponent: () => import('./pages/import/import').then((m) => m.ImportPage),
            },
            {
                path: 'budgets',
                loadComponent: () => import('./pages/budgets/budgets').then((m) => m.BudgetsPage),
            },
            {
                path: 'subscriptions',
                loadComponent: () =>
                    import('./pages/subscriptions/subscriptions').then((m) => m.SubscriptionsPage),
            },
            {
                path: 'assistant',
                loadComponent: () =>
                    import('./pages/assistant/assistant').then((m) => m.AssistantPage),
            },
            {
                path: 'settings',
                loadComponent: () =>
                    import('./pages/settings/settings').then((m) => m.SettingsPage),
            },
            { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
        ],
    },
    { path: '**', redirectTo: 'dashboard' },
];
