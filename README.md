# PocketLense

A personal budgeting app built with Angular, ASP.NET Core, C#, and PostgreSQL.
Import CSV, Excel, and PDF statements, organize transactions, set monthly budgets, and track recurring charges.

## Run locally

Requirements: .NET 10 SDK, Node.js 22.22.3+, npm, and Docker Desktop.

1. Copy `.env.example` to `.env` and choose a local database password.
2. Store the matching database connection and a random signing key outside the repository:

```sh
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5437;Database=pocketlense;Username=pocketlense;Password=YOUR_LOCAL_PASSWORD" --project src/PocketLense.Api
dotnet user-secrets set "Jwt:SigningKey" "YOUR_RANDOM_KEY_AT_LEAST_32_BYTES" --project src/PocketLense.Api
```

3. Start the database and API:

```sh
docker compose up -d
dotnet restore
dotnet tool restore
dotnet ef database update --project src/PocketLense.Infrastructure --startup-project src/PocketLense.Api
dotnet run --project src/PocketLense.Api
```

4. In another terminal:

```sh
cd web
npm ci
npm start
```

Open http://127.0.0.1:4200 and create an account. Angular forwards API requests to port 5080.
The database is exposed only on localhost, port 5437.


## Demo

Set your own demo password in user secrets, then start the API with the seed option:

```sh
dotnet user-secrets set "Demo:Password" "YOUR_DEMO_PASSWORD" --project src/PocketLense.Api
dotnet run --project src/PocketLense.Api -- --seed-demo
```

Login email: `demo@pocketlense.dev`. The password needs 8+ characters, uppercase, lowercase, a number, and a symbol.
Seeding runs only in Development and does nothing when the demo account already exists.

## Project structure

- `src/PocketLense.Api`: controllers, request validation, login, and optional assistant.
- `src/PocketLense.Core`: plain C# models, budget calculations, category matching, and subscription detection.
- `src/PocketLense.Infrastructure`: PostgreSQL access, CSV parsing, reporting, and demo data.
- `web/src/app/pages`: Angular pages with separate HTML, CSS, and TypeScript.
- `tests`: unit tests and real PostgreSQL integration tests.

## Checks

```sh
dotnet test
cd web
npm run build
```

Integration tests require Docker. On macOS, if Testcontainers cannot find Docker:

```sh
DOCKER_HOST=unix://$HOME/.docker/run/docker.sock dotnet test
```

## Optional assistant

The assistant uses the official Anthropic SDK through `IChatClient`. It is disabled by default.
Set `Ai:Enabled` to `true`, store `Ai:ApiKey` in user secrets, and set `Ai:Model` to a model available to your account.
The panel remains hidden when the key or model is missing. No real model calls are made by the tests.

Only four aggregate reporting functions are exposed. User identity comes from the authenticated request; the model cannot modify data or request another user's records. Requests are limited to 20 per user per hour and five tool-call rounds.

PocketLense Chat is available without an API key. Its preview mode uses simple rules and the same dashboard totals to answer a small set of budgeting questions. It clearly identifies itself as a preview. The existing assistant endpoint can replace this local response logic when Claude is enabled later.

## Statement files

- CSV and Excel files show their first worksheet or table so you can match columns before importing.
- Text-based PDFs are scanned for rows that begin with a full date and end with an amount.
- Scanned image PDFs, password-protected PDFs, and PDFs without a year in each transaction date need OCR or a bank-specific parser and are rejected with a clear message.
- Always check the preview before committing an import because bank statement formats vary.

## Version-one choices

- USD only. Expenses are negative; income is positive. Transfer categories never count toward spending or income.
- Spending is gross expenses. Positive refunds do not reduce the spending total; categorize them under the original expense category to keep them out of income.
- Credit-card payments between your own accounts belong in Transfers.
- Copying budgets fills missing categories and preserves existing limits. Category replacement combines overlapping budget limits.
- Statement imports reject the whole file for parsing errors. Reimporting the same statement skips its transactions. With overlapping exports, identical purchases are distinguished by their occurrence within each file; review ambiguous overlaps in Transactions.
- Subscription matches need confirmation. Projected renewals are estimates, not proof that a payment happened. Dismissed merchants are not suggested again.
- JWT sessions last one hour. Refresh tokens, password reset, bank/email connections, PDF imports, and deployment are future work.

Dependencies are pinned. Keep `.env`, local login files, and user secrets out of source control.
