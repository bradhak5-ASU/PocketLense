import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { HttpErrorResponse } from '@angular/common/http';
import { ApiService } from '../../services/api.service';
import { Account, ImportResult } from '../../models';

@Component({
    selector: 'app-import',
    imports: [CommonModule, FormsModule, RouterLink, MatButtonModule],
    templateUrl: './import.html',
})
export class ImportPage implements OnInit {
    private api = inject(ApiService);
    file: File | null = null;
    accounts: Account[] = [];
    accountId = '';
    headers: string[] = [];
    rows: string[][] = [];
    busy = false;
    error = '';
    result: ImportResult | null = null;
    fileType = '';
    amountMode = 'single';
    mapping = {
        dateColumn: '',
        dateFormat: 'MM/dd/yyyy',
        descriptionColumn: '',
        amountColumn: '',
        debitColumn: '',
        creditColumn: '',
        invertSign: false,
    };
    async ngOnInit(): Promise<void> {
        try {
            this.accounts = await this.api.get<Account[]>('accounts');
            this.accountId = this.accounts[0]?.id || '';
        } catch (error) {
            this.error = this.api.error(error);
        }
    }
    choose(event: Event): void {
        this.file = (event.target as HTMLInputElement).files?.[0] || null;
        this.headers = [];
        this.rows = [];
        this.result = null;
        this.error = '';
        this.fileType = this.file?.name.split('.').pop()?.toUpperCase() || '';
        if (this.file && this.file.size > 5 * 1024 * 1024) {
            this.error = 'Choose a statement smaller than 5 MB.';
            this.file = null;
            return;
        }
        const allowed = ['CSV', 'XLS', 'XLSX', 'PDF'];
        if (this.file && !allowed.includes(this.fileType)) {
            this.error = 'Choose a CSV, Excel, or PDF statement.';
            this.file = null;
        }
    }
    async preview(): Promise<void> {
        if (!this.file) return;
        this.busy = true;
        this.error = '';
        this.result = null;
        const form = new FormData();
        form.append('file', this.file);
        try {
            const preview = await this.api.post<{ headers: string[]; rows: string[][] }>(
                'imports/preview',
                form,
            );
            this.headers = preview.headers;
            this.rows = preview.rows;
            const find = (term: string) =>
                this.headers.find((h) => h.toLowerCase().includes(term)) || '';
            this.mapping.dateColumn = find('date');
            this.mapping.descriptionColumn = find('description');
            this.mapping.amountColumn = find('amount');
            this.mapping.debitColumn = find('debit');
            this.mapping.creditColumn = find('credit');
            if (this.fileType === 'PDF') this.mapping.dateFormat = 'yyyy-MM-dd';
        } catch (error) {
            this.error = this.api.error(error);
        } finally {
            this.busy = false;
        }
    }
    async commit(): Promise<void> {
        if (!this.file) return;
        this.busy = true;
        this.error = '';
        this.result = null;
        const form = new FormData();
        form.append('file', this.file);
        form.append('accountId', this.accountId);
        form.append(
            'mapping',
            JSON.stringify({
                ...this.mapping,
                amountColumn: this.amountMode === 'single' ? this.mapping.amountColumn : null,
            }),
        );
        try {
            this.result = await this.api.post<ImportResult>('imports/commit', form);
        } catch (error) {
            if (error instanceof HttpErrorResponse && Array.isArray(error.error?.errors))
                this.result = error.error;
            else this.error = this.api.error(error);
        } finally {
            this.busy = false;
        }
    }
}
