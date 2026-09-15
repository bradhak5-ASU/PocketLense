export interface Account {
    id: string;
    name: string;
    type: string;
}
export interface Category {
    id: string;
    name: string;
    kind: string;
}
export interface Rule {
    id: string;
    keyword: string;
    categoryId: string;
}
export interface Transaction {
    id: string;
    date: string;
    description: string;
    amount: number;
    accountId: string;
    categoryId: string | null;
}
export interface Budget {
    id: string;
    categoryId: string;
    category: string;
    limit: number;
    spent: number;
    remaining: number;
    percentUsed: number;
    status: string;
}
export interface Subscription {
    id: string;
    displayName: string;
    amount: number;
    previousAmount: number | null;
    frequency: string;
    nextDate: string;
    status: string;
    isManual: boolean;
}
export interface Bill {
    id: string;
    displayName: string;
    amount: number;
    nextDate: string;
}
export interface Dashboard {
    comparison: { spending: number; previousSpending: number; percentChange: number | null };
    income: number;
    monthlySubscriptions: number;
    categories: { category: string; amount: number }[];
    trends: { month: string; amount: number }[];
    budgets: Budget[];
    upcoming: Bill[];
    recent: { id: string; date: string; description: string; amount: number; category: string }[];
}
export interface SubscriptionList {
    items: Subscription[];
    monthlyCost: number;
    annualCost: number;
    upcoming: Bill[];
}
export interface ImportResult {
    imported: number;
    skippedDuplicates: number;
    errors: { row: number; reason: string }[];
}
export function currentMonth(): string {
    const date = new Date();
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
}
export function today(): string {
    const date = new Date();
    return `${currentMonth()}-${String(date.getDate()).padStart(2, '0')}`;
}
