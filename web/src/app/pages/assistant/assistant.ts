import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, ElementRef, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { ApiService } from '../../services/api.service';
import { Dashboard, currentMonth } from '../../models';

interface Message {
    role: 'assistant' | 'user';
    text: string;
}

@Component({
    selector: 'app-assistant-page',
    imports: [CommonModule, FormsModule, MatButtonModule],
    templateUrl: './assistant.html',
    styleUrl: './assistant.css',
})
export class AssistantPage {
    private api = inject(ApiService);
    private changeDetector = inject(ChangeDetectorRef);
    @ViewChild('chatEnd') chatEnd?: ElementRef<HTMLDivElement>;
    question = '';
    busy = false;
    error = '';
    messages: Message[] = [
        {
            role: 'assistant',
            text: "Hi, I'm PocketLense. I can explain this month's spending, budgets, and recurring charges. This preview uses simple rules with your dashboard data; Claude will be connected later.",
        },
    ];
    suggestions = [
        'Where did I spend the most this month?',
        'Which budgets need attention?',
        'What recurring charges do I have?',
        'How can I improve my spending this month?',
    ];

    useSuggestion(question: string): void {
        this.question = question;
        void this.send();
    }

    async send(): Promise<void> {
        const question = this.question.trim();
        if (!question || this.busy) return;
        this.messages.push({ role: 'user', text: question });
        this.question = '';
        this.error = '';
        this.scroll();

        if (['hi', 'hello', 'hey'].includes(question.toLowerCase())) {
            this.messages.push({
                role: 'assistant',
                text: 'Hi! Ask me about your spending, budgets, income, or recurring charges.',
            });
            this.scroll();
            return;
        }

        this.busy = true;
        try {
            const data = await this.api.get<Dashboard>('dashboard?month=' + currentMonth());
            this.messages.push({ role: 'assistant', text: this.answer(question, data) });
        } catch (error) {
            this.error = this.api.error(error);
        } finally {
            this.busy = false;
            this.changeDetector.detectChanges();
            this.scroll();
        }
    }

    clear(): void {
        this.messages = this.messages.slice(0, 1);
        this.error = '';
    }

    private answer(question: string, data: Dashboard): string {
        const words = question.toLowerCase();
        const top = data.categories[0];
        const attention = data.budgets.filter((budget) => budget.status !== 'OnTrack');

        if (
            words.includes('recurring') ||
            words.includes('subscription') ||
            words.includes('due')
        ) {
            const due = data.upcoming.map(
                (bill) => `${bill.displayName} (${this.money(bill.amount)})`,
            );
            const upcoming = due.length
                ? ` Due in the next 7 days: ${due.join(', ')}.`
                : ' Nothing is due in the next 7 days.';
            return `Your confirmed subscriptions cost about ${this.money(data.monthlySubscriptions)} per month.${upcoming}`;
        }

        if (words.includes('budget') || words.includes('attention') || words.includes('status')) {
            if (!data.budgets.length) return 'You have not set any budgets for this month yet.';
            if (!attention.length) return 'All of your budgets are currently on track.';
            return `These budgets need attention: ${attention.map((item) => `${item.category} is ${item.percentUsed}% used`).join(', ')}.`;
        }

        if (words.includes('most') || words.includes('top') || words.includes('where')) {
            return top
                ? `Your highest spending category this month is ${top.category} at ${this.money(top.amount)}.`
                : 'There is no spending data for this month yet.';
        }

        if (words.includes('improve') || words.includes('better') || words.includes('save')) {
            if (!top)
                return 'Start by importing transactions and setting category budgets. Then I can point out where your plan needs attention.';
            const budgetNote = attention.length
                ? ` Review ${attention.map((item) => item.category).join(' and ')} because those limits are close to or over budget.`
                : ' Your current budgets are on track.';
            return `Start with ${top.category}, your largest category at ${this.money(top.amount)}.${budgetNote} Also review confirmed subscriptions totaling ${this.money(data.monthlySubscriptions)} per month.`;
        }

        return `This month you spent ${this.money(data.comparison.spending)} and received ${this.money(data.income)} in income.${top ? ` Your largest category is ${top.category}.` : ''} Ask me about budgets, recurring charges, or ways to improve.`;
    }

    private money(value: number): string {
        return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value);
    }

    private scroll(): void {
        setTimeout(() => this.chatEnd?.nativeElement.scrollIntoView({ behavior: 'smooth' }));
    }
}
