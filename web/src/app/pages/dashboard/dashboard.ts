import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { ChartData, ChartOptions } from 'chart.js';
import { ApiService } from '../../services/api.service';
import { Dashboard, currentMonth } from '../../models';

@Component({
    selector: 'app-dashboard',
    imports: [CommonModule, FormsModule, RouterLink, MatButtonModule, BaseChartDirective],
    templateUrl: './dashboard.html',
    styleUrl: './dashboard.css',
    providers: [provideCharts(withDefaultRegisterables())],
})
export class DashboardPage implements OnInit {
    private api = inject(ApiService);
    private changeDetector = inject(ChangeDetectorRef);
    month = currentMonth();
    data: Dashboard | null = null;
    error = '';
    aiEnabled = false;
    question = '';
    answer = '';
    aiError = '';
    asking = false;
    busy = false;
    donut: ChartData<'doughnut'> = { labels: [], datasets: [] };
    trend: ChartData<'bar'> = { labels: [], datasets: [] };
    budgets: ChartData<'bar'> = { labels: [], datasets: [] };
    donutOptions: ChartOptions<'doughnut'> = {
        responsive: true,
        maintainAspectRatio: false,
        cutout: '73%',
        plugins: {
            legend: {
                position: 'right',
                labels: { boxWidth: 8, boxHeight: 8, padding: 18, font: { size: 11 } },
            },
        },
    };
    barOptions: ChartOptions<'bar'> = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
            x: {
                grid: { display: false },
                ticks: { font: { size: 10 }, color: this.color('--muted') },
            },
            y: {
                beginAtZero: true,
                ticks: {
                    font: { size: 10 },
                    color: this.color('--muted'),
                    callback: (value) => '$' + value,
                },
                grid: { color: this.color('--border') },
            },
        },
    };
    budgetOptions: ChartOptions<'bar'> = {
        ...this.barOptions,
        plugins: { legend: { position: 'bottom', labels: { boxWidth: 8, font: { size: 10 } } } },
    };

    async ngOnInit(): Promise<void> {
        void this.load();
        try {
            this.aiEnabled = (await this.api.get<{ aiAssistant: boolean }>('features')).aiAssistant;
        } catch {
            this.aiEnabled = false;
        }
    }
    async ask(): Promise<void> {
        this.asking = true;
        this.aiError = '';
        this.answer = '';
        try {
            this.answer = (
                await this.api.post<{ answer: string }>('assistant/ask', {
                    question: this.question,
                })
            ).answer;
        } catch (error) {
            this.aiError = this.api.error(error);
        } finally {
            this.asking = false;
        }
    }
    async load(): Promise<void> {
        this.busy = true;
        this.error = '';
        try {
            this.data = await this.api.get<Dashboard>('dashboard?month=' + this.month);
            this.donut = {
                labels: this.data.categories.map((c) => c.category),
                datasets: [
                    {
                        data: this.data.categories.map((c) => c.amount),
                        backgroundColor: [
                            '#4b83e6',
                            '#8aaff0',
                            '#73b7b4',
                            '#c1a5d7',
                            '#e6bd86',
                            '#a6bbd6',
                            '#e6a5a2',
                            '#94bd9e',
                            '#cebca3',
                            '#c5cbd5',
                        ],
                        borderWidth: 3,
                        borderColor: this.color('--panel'),
                    },
                ],
            };
            this.trend = {
                labels: this.data.trends.map((t) =>
                    new Date(t.month + '-01T00:00:00').toLocaleString('en-US', { month: 'short' }),
                ),
                datasets: [
                    {
                        data: this.data.trends.map((t) => t.amount),
                        backgroundColor: this.data.trends.map((_, i) =>
                            i === 5 ? '#4b83e6' : '#dbe7fa',
                        ),
                        borderRadius: 4,
                        maxBarThickness: 34,
                    },
                ],
            };
            this.budgets = {
                labels: this.data.budgets.map((b) => b.category),
                datasets: [
                    {
                        label: 'Budget',
                        data: this.data.budgets.map((b) => b.limit),
                        backgroundColor: '#dbe7fa',
                        borderRadius: 3,
                    },
                    {
                        label: 'Spent',
                        data: this.data.budgets.map((b) => b.spent),
                        backgroundColor: '#4b83e6',
                        borderRadius: 3,
                    },
                ],
            };
        } catch (error) {
            this.error = this.api.error(error);
        } finally {
            this.busy = false;
            this.changeDetector.detectChanges();
        }
    }

    private color(name: string): string {
        return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    }
}
