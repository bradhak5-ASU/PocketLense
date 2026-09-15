import { Injectable } from '@angular/core';

export type AppTheme = 'light' | 'dim' | 'black';

@Injectable({ providedIn: 'root' })
export class ThemeService {
    theme: AppTheme = this.readTheme();

    constructor() {
        this.apply(this.theme);
    }

    setTheme(theme: AppTheme): void {
        this.theme = theme;
        localStorage.setItem('pocketlense-theme', theme);
        this.apply(theme);
    }

    private apply(theme: AppTheme): void {
        document.documentElement.dataset['theme'] = theme;
    }

    private readTheme(): AppTheme {
        const saved = localStorage.getItem('pocketlense-theme');
        return saved === 'dim' || saved === 'black' ? saved : 'light';
    }
}
