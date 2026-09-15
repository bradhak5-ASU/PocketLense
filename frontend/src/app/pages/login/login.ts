import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../services/auth.service';
import { ApiService } from '../../services/api.service';

@Component({
    selector: 'app-login',
    imports: [FormsModule, RouterLink, MatButtonModule],
    templateUrl: './login.html',
    styleUrl: './login.css',
})
export class Login {
    private router = inject(Router);
    private auth = inject(AuthService);
    private api = inject(ApiService);
    email = '';
    password = '';
    error = '';
    busy = false;
    get register(): boolean {
        return this.router.url === '/register';
    }

    async submit(): Promise<void> {
        this.error = '';
        this.busy = true;
        try {
            await this.auth.submit(this.email, this.password, this.register);
        } catch (error) {
            this.error = this.api.error(error);
        } finally {
            this.busy = false;
        }
    }
}
