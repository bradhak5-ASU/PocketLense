import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ApiService } from '../../services/api.service';
import { Budget, Category, currentMonth } from '../../models';

@Component({
    selector: 'app-budgets',
    imports: [CommonModule, FormsModule, MatButtonModule, MatProgressBarModule],
    templateUrl: './budgets.html',
    styleUrl: './budgets.css',
})
export class BudgetsPage implements OnInit {
    private api = inject(ApiService);
    month = currentMonth();
    budgets: Budget[] = [];
    categories: Category[] = [];
    categoryId = '';
    limit = 300;
    editing = false;
    busy = false;
    error = '';
    message = '';
    async ngOnInit(): Promise<void> {
        try {
            this.categories = (await this.api.get<Category[]>('categories')).filter(
                (c) => c.kind === 'Expense',
            );
            this.categoryId = this.categories[0]?.id || '';
            await this.load();
        } catch (error) {
            this.error = this.api.error(error);
        }
    }
    async load(): Promise<void> {
        this.error = '';
        try {
            this.budgets = await this.api.get<Budget[]>('budgets?month=' + this.month);
        } catch (error) {
            this.error = this.api.error(error);
        }
    }
    edit(budget?: Budget): void {
        this.categoryId = budget?.categoryId || this.categories[0]?.id || '';
        this.limit = budget?.limit || 300;
        this.editing = true;
    }
    async save(): Promise<void> {
        this.busy = true;
        try {
            await this.api.put('budgets', {
                categoryId: this.categoryId,
                month: this.month + '-01',
                limit: this.limit,
            });
            this.editing = false;
            await this.load();
        } catch (error) {
            this.error = this.api.error(error);
        } finally {
            this.busy = false;
        }
    }
    async copy(): Promise<void> {
        this.busy = true;
        try {
            const result = await this.api.post<{ copied: number }>(
                'budgets/copy-previous?month=' + this.month,
            );
            this.message = `${result.copied} budgets copied. Existing limits were kept.`;
            await this.load();
        } catch (error) {
            this.error = this.api.error(error);
        } finally {
            this.busy = false;
        }
    }
}
