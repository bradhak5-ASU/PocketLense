import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
    const auth = inject(AuthService);
    if (request.url.startsWith('/api/') && auth.token) {
        request = request.clone({ setHeaders: { Authorization: `Bearer ${auth.token}` } });
    }
    return next(request).pipe(
        catchError((error) => {
            if (error.status === 401 && !request.url.startsWith('/api/auth/')) auth.logout();
            return throwError(() => error);
        }),
    );
};
