import { Component, inject, OnInit } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ApiService } from '../../services/api.service';

@Component({
    selector: 'app-shell',
    imports: [RouterLink, RouterLinkActive, RouterOutlet],
    templateUrl: './shell.html',
    styleUrl: './shell.css',
})
export class Shell implements OnInit {
    auth = inject(AuthService);
    private api = inject(ApiService);
    links = [
        { path: '/dashboard', label: 'Overview', icon: '◫' },
        { path: '/transactions', label: 'Transactions', icon: '⇄' },
        { path: '/budgets', label: 'Budgets', icon: '▤' },
        { path: '/subscriptions', label: 'Subscriptions', icon: '↻' },
        { path: '/assistant', label: 'PocketLense Chat', icon: '✦' },
        { path: '/import', label: 'Import statement', icon: '↥' },
        { path: '/settings', label: 'Settings', icon: '⚙' },
    ];
    async ngOnInit(): Promise<void> {
        try {
            await this.api.get('features');
        } catch {
            // I let the page show the connection error.
        }
    }
}
