import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { ApiService } from '../../services/api.service';
import { SubscriptionList, today } from '../../models';

@Component({
    selector: 'app-subscriptions',
    imports: [CommonModule, FormsModule, MatButtonModule],
    templateUrl: './subscriptions.html',
})
export class SubscriptionsPage implements OnInit {
    private api = inject(ApiService);
    data: SubscriptionList = { items: [], monthlyCost: 0, annualCost: 0, upcoming: [] };
    error = '';
    busy = false;
    editing = false;
    showDismissed = false;
    form = { displayName: '', amount: 0, frequency: 'Monthly', nextDate: today() };
    ngOnInit(): void {
        void this.load();
    }
    async load(): Promise<void> {
        try {
            this.data = await this.api.get<SubscriptionList>('subscriptions');
        } catch (error) {
            this.error = this.api.error(error);
        }
    }
    async action(path: string, body: unknown = {}): Promise<void> {
        this.busy = true;
        this.error = '';
        try {
            await this.api.post('subscriptions/' + path, body);
            await this.load();
        } catch (error) {
            this.error = this.api.error(error);
        } finally {
            this.busy = false;
        }
    }
    async save(): Promise<void> {
        await this.action('', this.form);
        if (!this.error) {
            this.editing = false;
            this.form = { displayName: '', amount: 0, frequency: 'Monthly', nextDate: today() };
        }
    }
}
