import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { ApiService } from '../../services/api.service';
import { Account, Category, Transaction, currentMonth, today } from '../../models';

@Component({
    selector: 'app-transactions',
    imports: [CommonModule, FormsModule, RouterLink, MatButtonModule],
    templateUrl: './transactions.html',
})
export class TransactionsPage implements OnInit {
    private api = inject(ApiService);
    private route = inject(ActivatedRoute);
    accounts: Account[] = [];
    categories: Category[] = [];
    items: Transaction[] = [];
    month = currentMonth();
    accountId = '';
    categoryId = '';
    search = '';
    page = 1;
    total = 0;
    loading = false;
    saving = false;
    error = '';
    message = '';
    editing = false;
    deleteId = '';
    ruleKeyword = '';
    makeRule = false;
    form = this.blank();

    blank(): Transaction {
        return {
            id: '',
            date: today(),
            description: '',
            amount: 0,
            accountId: this.accounts[0]?.id || '',
            categoryId: null,
        };
    }
    async ngOnInit(): Promise<void> {
        try {
            [this.accounts, this.categories] = await Promise.all([
                this.api.get<Account[]>('accounts'),
                this.api.get<Category[]>('categories'),
            ]);
            if (this.route.snapshot.queryParamMap.get('add')) this.edit();
            await this.load();
        } catch (error) {
            this.error = this.api.error(error);
        }
    }
    async load(reset = false): Promise<void> {
        if (reset) this.page = 1;
        this.loading = true;
        this.error = '';
        const query = new URLSearchParams({
            month: this.month,
            page: String(this.page),
            search: this.search,
        });
        if (this.accountId) query.set('accountId', this.accountId);
        if (this.categoryId) query.set('categoryId', this.categoryId);
        try {
            const result = await this.api.get<{ items: Transaction[]; total: number }>(
                'transactions?' + query,
            );
            this.items = result.items;
            this.total = result.total;
        } catch (error) {
            this.error = this.api.error(error);
        } finally {
            this.loading = false;
        }
    }
    edit(item?: Transaction): void {
        this.form = item ? { ...item } : this.blank();
        this.editing = true;
        this.makeRule = false;
        this.ruleKeyword = item?.description || '';
    }
    categoryName(id: string | null): string {
        return this.categories.find((c) => c.id === id)?.name || 'Uncategorized';
    }
    accountName(id: string): string {
        return this.accounts.find((a) => a.id === id)?.name || '';
    }
    async save(): Promise<void> {
        this.saving = true;
        this.error = '';
        this.message = '';
        try {
            if (this.form.id) await this.api.put('transactions/' + this.form.id, this.form);
            else await this.api.post('transactions', this.form);
            this.editing = false;
            this.message = 'Transaction saved.';
            if (this.makeRule && this.form.categoryId && this.ruleKeyword.trim()) {
                try {
                    await this.api.post('rules', {
                        keyword: this.ruleKeyword,
                        categoryId: this.form.categoryId,
                    });
                } catch (error) {
                    this.message += ' The rule could not be saved: ' + this.api.error(error);
                }
            }
            await this.load();
        } catch (error) {
            this.error = this.api.error(error);
        } finally {
            this.saving = false;
        }
    }
    async remove(): Promise<void> {
        this.saving = true;
        try {
            await this.api.delete('transactions/' + this.deleteId);
            this.deleteId = '';
            await this.load();
        } catch (error) {
            this.error = this.api.error(error);
        } finally {
            this.saving = false;
        }
    }
    async changeCategory(item: Transaction, value: string): Promise<void> {
        try {
            const updated = await this.api.put<Transaction>('transactions/' + item.id, {
                ...item,
                categoryId: value || null,
            });
            Object.assign(item, updated);
            this.message = 'Category saved. Use Edit to create a rule for similar descriptions.';
        } catch (error) {
            this.error = this.api.error(error);
        }
    }
}
