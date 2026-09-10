import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { Account, Category, Rule } from '../../models';
import { AppTheme, ThemeService } from '../../services/theme.service';

@Component({
    selector: 'app-settings',
    imports: [CommonModule, FormsModule, MatButtonModule, RouterLink],
    templateUrl: './settings.html',
    styleUrl: './settings.css',
})
export class SettingsPage implements OnInit {
    private api = inject(ApiService);
    theme = inject(ThemeService);
    accounts: Account[] = [];
    categories: Category[] = [];
    rules: Rule[] = [];
    account = { id: '', name: '', type: 'Checking' };
    category = { id: '', name: '', kind: 'Expense' };
    rule = { keyword: '', categoryId: '' };
    deleting: Category | null = null;
    replacementId = '';
    error = '';
    message = '';
    busy = false;
    setTheme(theme: AppTheme): void {
        this.theme.setTheme(theme);
    }
    ngOnInit(): void {
        void this.load();
    }
    async load(): Promise<void> {
        try {
            [this.accounts, this.categories, this.rules] = await Promise.all([
                this.api.get<Account[]>('accounts'),
                this.api.get<Category[]>('categories'),
                this.api.get<Rule[]>('rules'),
            ]);
        } catch (error) {
            this.error = this.api.error(error);
        }
    }
    categoryName(id: string): string {
        return this.categories.find((c) => c.id === id)?.name || '';
    }
    async saveAccount(): Promise<void> {
        await this.change(() =>
            this.account.id
                ? this.api.put('accounts/' + this.account.id, this.account)
                : this.api.post('accounts', this.account),
        );
        if (!this.error) this.account = { id: '', name: '', type: 'Checking' };
    }
    async saveCategory(): Promise<void> {
        await this.change(() =>
            this.category.id
                ? this.api.put('categories/' + this.category.id, this.category)
                : this.api.post('categories', this.category),
        );
        if (!this.error) this.category = { id: '', name: '', kind: 'Expense' };
    }
    async saveRule(): Promise<void> {
        await this.change(() => this.api.post('rules', this.rule));
        if (!this.error) this.rule = { keyword: '', categoryId: '' };
    }
    async remove(path: string): Promise<void> {
        await this.change(() => this.api.delete(path));
    }
    async removeCategory(): Promise<void> {
        if (!this.deleting) return;
        await this.remove(
            'categories/' +
                this.deleting.id +
                (this.replacementId ? '?replacementId=' + this.replacementId : ''),
        );
        if (!this.error) this.deleting = null;
    }
    async applyRules(): Promise<void> {
        await this.change(async () => {
            const result = await this.api.post<{ updated: number }>('rules/apply');
            this.message = `${result.updated} uncategorized transactions updated.`;
        });
    }
    private async change(action: () => Promise<unknown>): Promise<void> {
        this.busy = true;
        this.error = '';
        this.message = '';
        try {
            await action();
            await this.load();
        } catch (error) {
            this.error = this.api.error(error);
        } finally {
            this.busy = false;
        }
    }
}
