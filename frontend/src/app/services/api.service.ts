import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class ApiService {
    private http = inject(HttpClient);

    get<T>(path: string): Promise<T> {
        return firstValueFrom(this.http.get<T>('/api/' + path));
    }

    post<T>(path: string, body: unknown = {}): Promise<T> {
        return firstValueFrom(this.http.post<T>('/api/' + path, body));
    }

    put<T>(path: string, body: unknown): Promise<T> {
        return firstValueFrom(this.http.put<T>('/api/' + path, body));
    }

    delete(path: string): Promise<void> {
        return firstValueFrom(this.http.delete<void>('/api/' + path));
    }

    error(error: unknown): string {
        if (error instanceof HttpErrorResponse) {
            if (error.status === 0 || error.status === 504)
                return 'Cannot connect to PocketLense. Please check that the server is running.';
            const fields = error.error?.errors;
            if (fields && !Array.isArray(fields)) return Object.values(fields).flat().join(' ');
            return (
                error.error?.detail ||
                error.error?.title ||
                'Something went wrong. Please try again.'
            );
        }
        return 'Something went wrong. Please try again.';
    }
}
