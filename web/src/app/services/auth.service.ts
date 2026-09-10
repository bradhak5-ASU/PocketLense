import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from './api.service';

interface Session {
    token: string;
    email: string;
    expiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
    private api = inject(ApiService);
    private router = inject(Router);
    private session: Session | null = this.readSession();

    get token(): string | null {
        return this.session?.token ?? null;
    }
    get email(): string {
        return this.session?.email ?? '';
    }
    get loggedIn(): boolean {
        return this.session !== null && new Date(this.session.expiresAt).getTime() > Date.now();
    }

    async submit(email: string, password: string, register: boolean): Promise<void> {
        this.session = await this.api.post<Session>('auth/' + (register ? 'register' : 'login'), {
            email,
            password,
        });
        sessionStorage.setItem('pocketlense-session', JSON.stringify(this.session));
        await this.router.navigateByUrl('/dashboard');
    }

    logout(): void {
        this.session = null;
        sessionStorage.removeItem('pocketlense-session');
        void this.router.navigateByUrl('/login');
    }

    private readSession(): Session | null {
        try {
            return JSON.parse(sessionStorage.getItem('pocketlense-session') || 'null');
        } catch {
            return null;
        }
    }
}
