# PocketLense — Project Specification

PocketLense is a personal budgeting web app. Users log in, bring in their transactions (CSV statement import or manual entry), and track spending, budgets, and subscriptions through a dashboard with charts. A small, read-only AI assistant answers questions about the user's own data.

This document is the source of truth for requirements. If code and this document disagree, this document wins until it is updated.

---

## 1. Tech Stack

| Layer | Choice |
|---|---|
| Language / platform | C#, .NET 10 (LTS) |
| Backend | ASP.NET Core Web API (controllers, not minimal APIs) |
| Data access | Entity Framework Core + Npgsql |
| Database | PostgreSQL 17, run locally via Docker Compose |
| Auth | ASP.NET Core Identity + JWT bearer tokens |
| CSV parsing | CsvHelper |
| AI | `Microsoft.Extensions.AI` (`IChatClient`) with the official provider package: `Anthropic` (v10+) or `OpenAI` + `Microsoft.Extensions.AI.OpenAI` |
| Frontend | Angular (current stable), standalone components, Angular Material |
| Charts | Chart.js via ng2-charts |
| Tests | xUnit, `WebApplicationFactory`, Testcontainers (PostgreSQL) |

Do not use `Anthropic.SDK` or `tryAGI.Anthropic` — they are unofficial. Pin all NuGet and npm package versions.

---

## 2. Solution Structure

```
pocketlense/
├── src/
│   ├── PocketLense.Api/             controllers, auth, DI setup, Program.cs
│   ├── PocketLense.Core/            entities, interfaces, business logic (no EF, no web)
│   └── PocketLense.Infrastructure/  EF Core DbContext, migrations, AI client, transaction sources
├── tests/
│   ├── PocketLense.Tests.Unit/
│   └── PocketLense.Tests.Integration/
├── web/                       Angular app
├── docker-compose.yml
├── README.md                  short: what the project is and how to run it
└── PROJECT_SPEC.md            this file
```

Dependency direction: `Api → Infrastructure → Core`. `Core` references nothing else in the solution.

---

## 3. Conventions

- Money is `decimal` (`numeric(18,2)` in Postgres). Never `double` or `float`.
- Sign convention: expenses are negative, income is positive.
- Transaction dates are `DateOnly`. Audit timestamps (`CreatedAt`) are UTC `DateTime`.
- Nullable reference types enabled. Async all the way down.
- Controllers stay thin. Business rules live in `PocketLense.Core` services so they can be unit tested.
- Errors return RFC 7807 `ProblemDetails`. Validation failures return 400 with field-level errors.
- A resource that exists but belongs to another user returns **404**, never 403.
- Comments are minimal and only where the reason isn't obvious. No XML doc comments on every member.
- No secrets in the repo. JWT signing key and AI API key come from .NET user secrets (dev) or environment variables.

---

## 4. Data Model

Every user-owned table has a `UserId` column, and every query filters by the current user.

| Entity | Fields |
|---|---|
| **User** | ASP.NET Core Identity tables |
| **Account** | Id, UserId, Name, Type (`Checking`, `Credit`, `Savings`) |
| **Category** | Id, UserId, Name, Kind (`Expense`, `Income`, `Transfer`) |
| **CategoryRule** | Id, UserId, Keyword, CategoryId |
| **Transaction** | Id, UserId, AccountId, Date, Description, Amount, CategoryId (nullable), Source (`Manual`, `Csv`, `Email`), ImportHash (nullable), CreatedAt |
| **Budget** | Id, UserId, CategoryId, Month (first day of month, `DateOnly`), Limit |
| **Subscription** | Id, UserId, MerchantKey, DisplayName, Amount, PreviousAmount (nullable), Frequency (`Weekly`, `Monthly`, `Annual`), NextDate, Status (`Detected`, `Confirmed`, `Dismissed`), IsManual |

Unique index on `(UserId, ImportHash)` where `ImportHash` is not null. Unique index on `(UserId, CategoryId, Month)` for budgets.

---

## 5. Features

### F1. Authentication

**Requirements**
- Register with email + password (Identity defaults, minimum 8 characters).
- Login returns a JWT (60-minute lifetime) containing the user id.
- On registration, the user gets default categories: Housing, Utilities, Groceries, Dining, Transport, Shopping, Entertainment, Subscriptions, Health, Other (Expense); Income (Income); Transfers (Transfer). They also get one default account named "Checking".
- Angular: route guard blocks app pages when logged out; HTTP interceptor attaches the token; a 401 response logs the user out and redirects to login.

**Expected results**
- Registering with an existing email returns 400 with a clear message.
- Wrong password returns 401 with a generic message (does not reveal whether the email exists).
- User A requesting User B's transaction by id gets 404.

### F2. Accounts

**Requirements:** CRUD for accounts. An account with transactions cannot be deleted.

**Expected results:** Deleting an account that has transactions returns 409 with a message.

### F3. Manual Transactions

**Requirements**
- Create, edit, delete transactions: date, description, amount, account, category (optional).
- If no category is given, category rules are applied (F5).
- Transactions list supports filters: month, account, category, and text search on description. Paginated, newest first.

**Expected results**
- Amount of `0` or a missing date/description returns 400.
- A manually added `-12.50` "UBER TRIP" with a rule `UBER → Transport` is saved with category Transport.

### F4. CSV Statement Import

**Requirements**
- Two-step flow:
  1. `POST /api/imports/preview` — upload CSV (max 5 MB). Returns detected headers and the first 20 rows.
  2. `POST /api/imports/commit` — same file plus a column mapping and target account.
- Mapping options: date column + date format, description column, and either a single amount column **or** separate debit/credit columns, plus an "invert sign" toggle (credit card exports often flip signs).
- Import is all-or-nothing for parse errors: if any row fails to parse, nothing is saved and the response lists row numbers and reasons.
- Duplicate detection: `ImportHash` = SHA-256 of `AccountId | Date | Amount | normalized description | occurrence index`. The occurrence index counts identical rows within the same file, so two genuine identical coffees on the same day both import, but re-importing the same file imports nothing.
- Category rules are applied to imported rows.
- After a successful import, subscription detection (F7) runs for that user.

**Expected results**
- Commit response: `{ imported, skippedDuplicates, errors: [{ row, reason }] }`.
- Importing the same file twice: second response has `imported: 0` and `skippedDuplicates` equal to the row count.
- A debit/credit mapped file with debit `45.00` stores `-45.00`.

### F5. Categories and Rules

**Requirements**
- CRUD for categories and rules. A category in use cannot be deleted unless the user picks a replacement category to move its transactions to.
- Rule matching: case-insensitive "description contains keyword". If several rules match, the longest keyword wins.
- When a user changes a transaction's category in the UI, offer "Always categorize descriptions containing ___ as ___?" which creates a rule.
- `POST /api/rules/apply` re-applies rules to uncategorized transactions only.

**Expected results**
- Rules `AMAZON → Shopping` and `AMAZON PRIME → Subscriptions`: "AMAZON PRIME*2K4" matches Subscriptions.
- Re-applying rules never overwrites a category the user set manually.

### F6. Budget Tracker

**Requirements**
- Set a monthly limit per expense category. "Copy from previous month" creates the same budgets for the selected month.
- For a month, return per budget: limit, spent (sum of absolute values of expenses in that category), remaining, percent used, status.
- Status: `OnTrack` below 80%, `Warning` 80% to below 100%, `Over` at 100% or above.
- Transfer and Income categories are excluded from all spending totals.

**Expected results**
- Limit 300, spent 240 → 80%, `Warning`. Spent 300 → `Over`.
- UI shows a progress bar per category, colored by status, with a month picker.

### F7. Subscription Tracker

**Requirements**
- **Detection** groups expense transactions by `MerchantKey` (description uppercased, digits and punctuation removed, whitespace collapsed).
- A group is recurring when both conditions hold:
  - **Intervals:** the median gap between charges falls in one of these bands:
    - Weekly: 6–8 days
    - Monthly: 26–35 days
    - Annual: 350–380 days
  - **Amounts:** every amount is within 10% of the median amount.
- Minimum occurrences: 3 for Weekly/Monthly, 2 for Annual.
- `NextDate` = last charge date + 7 days, 1 month, or 1 year.
- **Price increase:** if the latest charge is more than 1% above the previous charge, set `PreviousAmount` and flag it.
- Detected subscriptions start as `Detected`. The user confirms or dismisses them. Dismissed merchants are never re-suggested.
- Users can add subscriptions manually (`IsManual = true`).
- Endpoints return upcoming renewals in the next 30 days, and total monthly and annual cost of confirmed subscriptions. Weekly costs × 52 / 12; annual costs / 12.

**Expected results**
- Netflix charged 15.49 on Jun 3, Jul 3, and Aug 3 → Monthly, NextDate Sep 3.
- Spotify 11.99, 11.99, 12.99 → detected, flagged with PreviousAmount 11.99.
- Two unrelated Uber rides 3 weeks apart → not detected.

### F8. Dashboard and Charts

**Requirements**
- Summary cards:
  - Total spending this month vs last month, with % change
  - Income this month
  - Confirmed subscription cost per month
- Charts:
  - Donut: spending by category for the selected month
  - Bar: total spending for each of the last 6 months
  - Grouped bar: budget limit vs spent per category
- List: bills and subscriptions due in the next 7 days.
- One aggregated endpoint `GET /api/dashboard?month=YYYY-MM` returns everything the dashboard needs.

**Expected results:** A new user with no data sees empty states ("No transactions yet — import a statement"), not errors or blank charts.

### F9. AI Assistant ("Ask PocketLense")

Deliberately minimal. The app must be fully usable without it.

**Requirements**
- One panel on the dashboard: a text box and an answer area. No chat history. Each question is independent.
- `POST /api/assistant/ask` with `{ question }` (max 500 characters) returns `{ answer }`.
- The model can call only these four read-only functions, registered as `IChatClient` tools:
  - `GetSpendingByCategory(month)`
  - `GetMonthComparison(month)`
  - `GetBudgetStatus(month)`
  - `GetUpcomingBills(days)`
- These functions reuse the same Core services as the dashboard.
- The user id is always taken from the JWT inside the server, never from model-supplied arguments.
- Maximum 5 tool-call rounds per question.
- The model does not receive raw transaction lists, only the aggregated function results.
- Answer behavior:
  - Use only numbers returned by the functions.
  - Say plainly when the data needed isn't available.
  - Give no investment advice.
  - Keep answers to about 120 words.
- Rate limit: 20 questions per user per hour (ASP.NET Core rate limiting). Exceeding it returns 429.
- The AI cannot create, change, or delete anything.

**Expected results**
- "Where did I spend the most this month?" → answer names the top category with the same amount the dashboard shows.
- "What's due this week?" → lists items matching the dashboard's upcoming list.
- "Should I buy Tesla stock?" → declines investment advice briefly.
- AI disabled or no API key → panel hidden, endpoint returns 404.

### F10. Email / Bank Sync (built, disabled)

**Requirements**
- Define `ITransactionSource` in Core. Manual entry, CSV import, and email sync each implement it.
- `EmailTransactionSource` exists as a placeholder class with no working logic.
- Feature flag `Features:EmailSync` defaults to `false`. When false, no email endpoints are registered.
- Settings page shows a disabled "Connect email" button labeled "Coming soon".

**Expected results:** Enabling the feature later requires implementing `EmailTransactionSource` and flipping the flag, with no changes to other sources, entities, or UI structure.

### F11. Feature Flags Endpoint

`GET /api/features` returns `{ aiAssistant: bool, emailSync: bool }`. The Angular app reads this on startup to show or hide features.

---

## 6. API Endpoints

All endpoints except auth require a valid JWT.

| Area | Endpoints |
|---|---|
| Auth | `POST /api/auth/register`, `POST /api/auth/login` |
| Accounts | `GET/POST /api/accounts`, `PUT/DELETE /api/accounts/{id}` |
| Transactions | `GET/POST /api/transactions`, `PUT/DELETE /api/transactions/{id}` |
| Imports | `POST /api/imports/preview`, `POST /api/imports/commit` |
| Categories | `GET/POST /api/categories`, `PUT/DELETE /api/categories/{id}` |
| Rules | `GET/POST /api/rules`, `DELETE /api/rules/{id}`, `POST /api/rules/apply` |
| Budgets | `GET /api/budgets?month=`, `PUT /api/budgets`, `POST /api/budgets/copy-previous?month=` |
| Subscriptions | `GET /api/subscriptions`, `POST /api/subscriptions`, `POST /api/subscriptions/{id}/confirm`, `POST /api/subscriptions/{id}/dismiss`, `POST /api/subscriptions/detect` |
| Dashboard | `GET /api/dashboard?month=` |
| Assistant | `POST /api/assistant/ask` |
| Features | `GET /api/features` |

---

## 7. Angular Screens

| Screen | Contents |
|---|---|
| Login / Register | Forms with validation messages |
| Dashboard | Summary cards, three charts, upcoming bills, Ask PocketLense panel (if enabled) |
| Transactions | Filterable, paginated table; add/edit dialog; inline category change with "create rule?" prompt |
| Import | Upload → preview table → column mapping → result summary |
| Budgets | Month picker, progress bars, set limits, copy previous month |
| Subscriptions | Detected (confirm/dismiss), confirmed list, upcoming renewals, monthly/annual totals, price-increase badges |
| Settings | Accounts, categories, rules, connected sources (disabled email button) |

---

## 8. Configuration

```json
{
  "ConnectionStrings": { "Default": "<set with user secrets>" },
  "Jwt": { "Issuer": "pocketlense", "Audience": "pocketlense", "SigningKey": "<user-secrets>" },
  "Ai": { "Enabled": true, "Provider": "Anthropic", "Model": "<model-id>", "ApiKey": "<user-secrets>" },
  "Features": { "EmailSync": false }
}
```

`Ai:Provider` is `Anthropic` or `OpenAI`. Only the registration of `IChatClient` changes between them.

---

## 9. Testing Requirements

**Unit tests (`PocketLense.Tests.Unit`)**
- **Budget math:** BudgetCalculator statuses at 79.99%, 80%, 99.99%, and 100%.
- **Rule matching:** longest-keyword wins, and matching is case-insensitive.
- **CSV mapping:** single amount column, debit/credit columns, invert sign, bad date row reported with its row number.
- **Import hash:** identical rows in one file both import; re-importing the same file imports nothing.
- **Subscription detection:** every example in F7, plus the annual case and a dismissed merchant not re-suggested.

**Integration tests (`PocketLense.Tests.Integration`, Testcontainers PostgreSQL)**
- Register → login → create transaction → fetch it.
- User isolation: User B gets 404 for User A's transaction, budget, and subscription.
- CSV import end to end, including duplicate re-import.
- AI endpoint returns 404 when `Ai:Enabled` is false.

AI answers are not tested against a live model. The tool functions are tested as normal Core services.

---

## 10. Demo Seed

Running the API with `--seed-demo` in Development creates `demo@pocketlense.dev` (password set in user secrets) with 3 months of realistic data:
- Rent, utilities, groceries, dining, transport, salary income, a transfer to savings
- Netflix, a gym membership, and Spotify with a price increase in the latest month
- Budgets where one category is `OnTrack`, one is `Warning`, and one is `Over`

---

## 11. Out of Scope (v1)

- Real email or bank connections
- AI actions or background AI insights
- PDF statement import
- Refresh tokens, email confirmation, password reset
- Multi-currency
- Azure deployment and CI (planned next)

---

## 12. Definition of Done

- `docker compose up -d`, `dotnet ef database update`, `dotnet run`, and `ng serve` bring the app up from a fresh clone, following README steps.
- All expected results in section 5 hold.
- All tests in section 9 pass.
- No secrets committed.
- The app works fully with `Ai:Enabled` set to `false`.
