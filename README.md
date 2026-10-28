# Northwind Support Desk

A support ticket system: raise, triage, escalate, search and filter tickets, with agent sign-in.
**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.
Assignment: [`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md). Step-by-step working notes: [`NOTES.md`](NOTES.md).

All four tasks are done: DEFECT-117 + server-side filtering, escalation & assignment, authentication,
and the Docker image. The optional SLA summary report (2.5) was not built.

---

## Run it

**Prerequisites:** Docker, Node.js 22 (`.nvmrc`; 20.19+ works). The .NET 10 SDK is only needed to
run the API or the tests outside Docker.

```bash
cp .env.example .env              # required - its values are working local fixtures
docker compose up --build         # SQL Server + API on http://localhost:5080 (migrates + seeds)
npm install
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web     # web app on http://localhost:5173
```
PowerShell: `$env:VITE_API_PROXY_TARGET='http://localhost:5080'; npm run web`

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API (container) | http://localhost:5080 · health: http://localhost:5080/health |
| Swagger | http://localhost:5080/swagger - only with `API_ENVIRONMENT=Development` in `.env` |
| SQL Server | `localhost,1433`, user `sa`, password `Local_Dev_Password1` |

### Development login

| Email | Password | |
| --- | --- | --- |
| `sara.lindqvist@northwind-support.example` | `LocalDevOnly-NotARealPassword-1` | |
| `alex.turner@` · `ben.osei@` · `priya.nair@` · `marcus.doyle@` `northwind-support.example` | same | |
| `yuki.tanaka@northwind-support.example` | same | inactive - **cannot** sign in |

The password is a local fixture from `.env` (`Seed__AgentPassword`), hashed at seed time. It only
applies to agents with no password yet; after changing it, run `docker compose down -v`.

### Running the API on the host instead (`npm run api`)

```bash
npm run db:up                     # SQL Server only (still reads .env)
dotnet user-secrets set Jwt:SigningKey "$(openssl rand -base64 48)" --project apps/api/src/SupportDesk.Presentation
dotnet user-secrets set Seed:AgentPassword "LocalDevOnly-NotARealPassword-1" --project apps/api/src/SupportDesk.Presentation
npm run api                       # https://localhost:7043, Swagger at /swagger
npm run web                       # proxies to https://localhost:7043 by default
```
Without `Jwt:SigningKey` the API refuses to start. Visual Studio: open `SupportDesk.sln`, start
**SupportDesk.Presentation** on the **https** profile.

### Tests

```bash
npm test          # dotnet test (unit, integration, architecture) then Vitest - no database needed
```
Current: **229 unit + 35 integration + 4 architecture** (.NET), **69** web. Lint, typecheck and
`dotnet format --verify-no-changes` are clean.

---

## What was built, and the rules as understood

| Task | What |
| --- | --- |
| 1.1 DEFECT-117 | Every filter change sends one request immediately; search is debounced; stale requests are cancelled |
| 1.2 Filtering | Status, priority, category, customer, agent, unassigned-only, search and SLA status - all in SQL, before count and paging |
| 2 Triage | On create: priority (BR-1), SLA window (BR-2/3), owner (BR-4/5), each with a reason. Nobody eligible → 201, unassigned, with the reason |
| 2 Escalation | One level up, due date restarts from now, owner re-checked, immutable history row. 400/404/409 as specified |
| 2 UI | One SLA indicator (list + detail), SLA filter, escalation panel + history, triage result after create |
| 3 Auth | Email + password login, JWT, every endpoint closed centrally, login page, protected routes, logout, 401 → login |
| 4 Docker | Multi-stage image, non-root on 8080, compose `api` service, `/health`, migrate + seed on an empty volume |

**Assumptions I made where the spec was open**
- **SLA boundaries:** unresolved exactly at the due time = *AtRisk* (Breached means strictly past due); exactly 25 % of the window left = *AtRisk*; resolved exactly at due = *Met*.
- **Rounding:** the window is rounded to whole minutes, then the 1 h floor applies (every tier).
- **Ties:** fewest open tickets, then lowest agent id. On escalation the current owner keeps the ticket if still eligible, and the escalated ticket doesn't count against them.
- **Filters combine with AND.** Unassigned-only + an agent → empty page, total 0. Search matches title, reference (`0042` also finds `TCK-10042`) or customer name, trimmed, case-insensitive on SQL Server.
- **Inactive category on create → 409.** Manual assignment (existing endpoint) is an override and does not apply the specialist rule.

---

## Design decisions (chosen, rejected, why)

**Where the business rules live.** Small pure policy classes in the Domain - `PriorityPolicy`,
`SlaWindowPolicy`, `AgentSelectionPolicy` - and one `TicketTriage` (Application) that both create and
escalate call, so the rules exist once. No interfaces on them: nothing swaps them, so an interface
would be ceremony. Interfaces stay at real seams (repositories, queries, clock, hasher, token issuer).

**Design patterns that fit**
1. **Policy objects** (strategy-like) - one rule per class, unit-tested directly, shared by create and escalate.
2. **Options pattern with startup validation** - `Sla`, `Jwt`, `Seed`, `Database` sections; bad SLA values or a missing/short signing key stop the app at startup, not at the first request.
3. **Specification as an expression** - `SlaStatusRule` is one `Expression<Func<Ticket, SlaStatus>>`, used in SQL for the projection *and* the filter, and compiled for unit tests. **Rejected:** a C# evaluator plus a separate SQL copy - two versions of one rule that can drift.

**SLA config moved to `appsettings.json`** (BR-6 "or justify"): it applied to Development only and
was never read. Now it's bound to `SlaOptions` for every environment and validated on start.

**Stored the applied SLA window** (`Tickets.SlaWindowMinutes`), not the status. AtRisk needs "25 % of
*this* ticket's window", and escalation restarts it. Status stays derived (BR-8).

**Wrapper responses:** create returns `{ ticket, triage }`, escalate `{ ticket, escalation }`, so the
UI updates from the response without a reload.

**Docker:** migrate/seed are switched on by `Database:MigrateOnStartup` / `Database:SeedDemoData`,
not by the environment name, so the container runs as **Production** and still prepares an empty
volume. **Rejected:** running the container as Development to get migrations (drags in Swagger and
dev config). Secrets are required in compose (`${VAR:?}`), never defaulted there.

---

## The defect (DEFECT-117)

**Root cause:** in `apps/web/src/features/tickets/hooks/useTicketList.ts` the effect that loads tickets
listed only `search`, `page` and `pageSize` as dependencies, and an `eslint-disable` hid the rest. On
page 1, changing status, priority, category, customer, agent, unassigned, sort or "Clear filters"
updated state but never re-ran the request; the next search change did, which is why an old filter
"suddenly" applied. On page 2 it worked, because changing a filter resets the page - hence "sometimes".

**Fix:** the effect depends on all filters. Only the search text is debounced (300 ms); any other
change fetches immediately and takes the half-typed search with it. The previous request is cancelled
with an `AbortController`. The `eslint-disable` is gone.

**Verify:** `npm run test --workspace apps/web` - the regression tests in `useTicketList.test.ts` run
the real API module with a mocked `fetch` and assert the URL sent. On the original code 9 of them
failed. By hand: DevTools → Network, on page 1 change any dropdown → one request, immediately.

## Filtering

**Where:** `TicketQueries.ApplyFilters`, on the joined entities (ticket + customer + category + agent)
*before* projecting to the DTO, so every filter is a SQL `WHERE` on real columns. Order:
`filter → COUNT → sort → Skip/Take → project`, so the total and the pager describe the filtered set.

**How the SQL was checked:** EF command logging in Development
(`Microsoft.EntityFrameworkCore.Database.Command`). `?priority=High&search=contoso` shows the same
`WHERE ... [t].[Priority] = @priority AND (... LIKE ...)` in both the `COUNT(*)` and the
`OFFSET/FETCH` query. Sensitive-data logging stays off.

**Adding the next filter:** one property on `TicketQuery`, one line in `ApplyFilters`:
`if (query.X is { } x) source = source.Where(t => ...);` - `slaStatus` was added exactly like this.

## Authentication

| Topic | Choice |
| --- | --- |
| Credential storage | Nullable `PasswordHash` column on `Agents` (NULL = no login). Only agents sign in, 1:1, no roles - a Users table would pay off only with non-agent users |
| Hashing | ASP.NET Core Identity `PasswordHasher`: PBKDF2-HMAC-SHA512, 100k iterations, random salt per hash, versioned format |
| Failures | Unknown email, wrong password, inactive agent → the same 401 and message. An unknown email still costs one hash, so timing doesn't reveal which emails exist |
| Token | JWT HS256, **8 h** (one shift), no refresh token. Key from `Jwt:SigningKey` (≥ 32 bytes, checked at startup). Clock skew 30 s |
| Enforcement | Fallback policy `RequireAuthenticatedUser` - every endpoint is closed unless it says otherwise. Open: `POST /api/auth/login`, `/health`, Swagger |
| `escalatedBy` | From the token's agent id; stored as an FK plus a name snapshot |
| Web storage | `sessionStorage`: survives a reload, not shared across tabs, gone when the tab closes. Trade-off: readable by XSS. An httpOnly cookie avoids that but needs cookie auth + CSRF protection |
| 401 handling | `client.ts` attaches the token; any 401 clears the session and sends the user to `/login`, then back to where they were |

**Before production:** a real key from a secret store and rotation, HTTPS, a shorter token with
refresh/revocation (a deactivated agent's token stays valid until expiry), lockout/rate limiting.

## Docker

```bash
cp .env.example .env && docker compose down -v && docker compose up --build
```

| Variable (`.env`) | Purpose |
| --- | --- |
| `MSSQL_SA_PASSWORD` | SQL Server sa password; compose builds the API's connection string from it (`Server=sqlserver`) |
| `Jwt__SigningKey` | Token signing key - **required**, `up` stops without it |
| `Seed__AgentPassword` | Dev password for seeded agents - **required** |
| `API_ENVIRONMENT` | API environment name, default `Production` (`Development` adds Swagger) |

Compose also sets `Database__MigrateOnStartup=true` and `Database__SeedDemoData=true`. The values in
`.env.example` are local fixtures - never for production. Nothing is baked into the image:
`appsettings.Development.json` (it holds the local sa password) is excluded by `.dockerignore`.

**Image:** `apps/api/Dockerfile`, two stages. Restore runs on the project files,
`Directory.*.props` and `global.json` before the source is copied. Runtime is `aspnet:10.0`, user
`app` (uid 1654), plain HTTP 8080, no certificates. **Size: 457 MB** vs **1.31 GB** for the SDK image.

**How each check was done**
| Check | Result |
| --- | --- |
| `docker compose down -v && docker compose up --build` | Log: 5 migrations applied, "Seeded 5 categories, 6 customers, 6 agents and 40 tickets". `/health` → 200 `{"status":"Healthy","checks":{"database":"Healthy"}}`. Login → token → `/api/tickets` total 40 |
| Restore cache | Added a line to one `.cs` file and rebuilt: `RUN dotnet restore` was `CACHED`, only copy + publish re-ran |
| Image size | `docker images`: `supportdesk-api` 457 MB, `mcr.microsoft.com/dotnet/sdk:10.0` 1.31 GB |
| Non-root | `docker compose exec api whoami` → `app` |
| Web against the container | `VITE_API_PROXY_TARGET=http://localhost:5080 npm run web`: login 200, `/api/tickets` 401 without a token |

**Migrations in a real deployment.** Not on app startup (`MigrateOnStartup=false`). The build produces
an EF Core migration bundle (`dotnet ef migrations bundle`) or an idempotent script
(`dotnet ef migrations script --idempotent`), and the pipeline runs it as its own step before the new
version rolls out. Why: several replicas starting together would race to migrate; a failed migration
should fail one visible step, not crash-loop the app; the script can be reviewed first; only that step
needs DDL rights, so the app can run as a least-privilege login; startup doesn't wait on a long
migration. Demo data is never seeded there.

## Database

| Change | Migration |
| --- | --- |
| `Tickets.SlaWindowMinutes` (int NULL, check > 0), backfilled from the existing due dates | `AddTicketSlaWindowMinutes` |
| `TicketEscalations` table: before/after priority, agent and due date, reason (500), who, when. FKs **Restrict** (history is never deleted with its ticket or agent); priority check constraints | `AddTicketEscalations` |
| `Agents.PasswordHash` (nvarchar 256, NULL) | `AddAgentPasswordHash` |
| `TicketEscalations.EscalatedByAgentId` (FK → Agents) + `EscalatedBy` widened to 200 (name snapshot) | `AddEscalatedByAgent` |

**Index:** `IX_TicketEscalations_TicketId_EscalatedAtUtc` on `(TicketId, EscalatedAtUtc DESC)`. It
serves the history query (one ticket, newest first) as a seek with no sort, and also covers the
`TicketId` foreign key, so no separate FK index is needed. The list filters use the indexes that were
already there (`Status+Priority`, `CustomerId`, `CategoryId`, `AssignedAgentId`, `DueAtUtc`).

All migrations apply cleanly to an empty database (checked by the Docker run above).

## Testing

| Suite | What it covers |
| --- | --- |
| Unit (xUnit + Moq, `FixedClock`, `TicketBuilder`) | Every policy incl. the 1 h floor, forced-Critical, specialists, limits, inactive, ties, nobody eligible; escalation (one step, due from now, owner kept/changed, history row, each rejection, ticket unchanged on 409); SLA status at the exact boundaries; options validators; hasher; seeder; login handler |
| SQLite in-memory (real SQL) | Each filter alone and combined, search on customer name/title/reference, count + paging of the filtered set, every SLA status in SQL, escalation history, candidate loading in one query |
| Integration (`WebApplicationFactory` + SQLite) | Login 401s identical, every endpoint 401 without a token and 200 with one, `escalatedBy` from the token, `/health` 200 without a token and 503 when the database is unreachable |
| Architecture | Layer dependencies, persistence model |
| Web (Vitest + RTL) | DEFECT-117 regression, short reason blocks submit, 409 shown with the ticket unchanged, every SLA state distinguishable, triage result, login/redirect/401/logout, token attached |

Auth tests were mutation-checked: removing the fallback policy fails 15 integration tests; removing
`AllowAnonymous` from `/health` fails both health tests.

**Not tested on purpose:** SQL Server collation (SQLite is case-sensitive; checked by hand on SQL
Server); SQL Server migrations in tests (SQLite uses `EnsureCreated`; covered by the Docker run);
end-to-end browser tests (checked by hand).

## AI usage

**Tools:** Claude Code in the repo (investigation, tests, implementation, Docker); Claude chat for
planning and reviewing diffs; Context7 for current library docs.

**Workflow that worked:** rules up front (no git commands, no unrelated changes, no `any` /
`@ts-ignore` / `eslint-disable`). Plan first with questions, I approve, then one small step at a time:
run the tests, list the changed files, stop. I read every diff and commit myself. For the defect:
tests first, confirmed failing on the original code, then the fix.

**Accepted as-is:** the investigation that located the defect in `useTicketList.ts` and the missing
filtering in `TicketQueries.cs` - confirmed by reading the code and reproducing it in the browser.

**Changed or rejected**
- Rejected `JSON.stringify(filters)` as the dependency: keeps the `eslint-disable` and debounces every filter.
- The first regression tests waited 300 ms before asserting, so a "debounce everything" fix would pass. Tightened them to assert immediately.
- Reverted unrelated edits to `.gitignore` and `package-lock.json`.
- The proposed Dockerfile had a comment after an instruction (`ENV X=8080  # ...`); Docker only treats `#` at the start of a line as a comment. Comments moved to their own lines.
- I required `.env.example` to hold working local values so `cp` + `up` works with zero edits.

**Bugs AI introduced (caught by tests or tooling):** a static field declared before the expression it
compiled (null at runtime); a rounding test using 0.725 (floating point); a mocked id of 0; CRLF line
endings written by a script (caught by `dotnet format`). **Found with AI:** a plain-text dev password
in the seeder and a signing key in `appsettings.Development.json` (both moved to configuration); the
`System.Security.Cryptography.Pkcs` vulnerability (pinned to 10.0.14); compose's required secrets
also blocking `npm run db:up`.

**How I verified generated code:** regression tests failing before and passing after; mutation checks
on the auth and health tests; generated SQL read in the EF log; the Docker checks above; manual
browser checks.

## Known limitations

- **Concurrency:** no `rowversion` on Ticket, so two escalations at the same moment can both apply, and two creates can both take an agent's last free slot.
- **Search:** leading-wildcard `LIKE` scans; at ~10 M tickets I'd use full-text search.
- **Auth:** no lockout or rate limiting; a deactivated agent's token works until it expires (only login and `/me` re-check); token in `sessionStorage` is readable by XSS; no refresh tokens, password reset or roles (out of scope).
- **Manual assignment** skips the specialist rule (treated as an override). Changing SLA config doesn't re-window existing tickets.
- **Docker:** `npm run db:up` also needs `.env` (compose checks the API's required variables for the whole file). Data Protection keys aren't persisted in the container - harmless, since auth uses JWT, not cookies. CORS origins are hard-coded to the Vite dev server.
- **Not built:** the optional SLA summary report (2.5). CI, caching, notifications etc. are out of scope.

## Commands

| Command | What it does |
| --- | --- |
| `docker compose up --build` / `down` / `down -v` | Start SQL Server + API / stop / stop and wipe data |
| `npm run db:up` / `db:down` / `db:reset` | SQL Server only: start / stop / wipe |
| `npm run api` / `npm run web` | API on the host / React app |
| `npm test` · `npm run lint` · `typecheck` · `format` | Tests · web lint · types · `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `required variable Jwt__SigningKey is missing` | `cp .env.example .env` |
| API cannot reach the database | Wait ~20 s for SQL Server; `docker compose ps` should show it healthy |
| Port 1433 or 5080 in use | Change the host port in `docker-compose.yml` |
| Login fails after changing `Seed__AgentPassword` | `docker compose down -v` (the seeder only sets passwords that are missing) |
| Web app shows "Unable to load tickets" | API not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| Browser warns about the certificate (host run) | `dotnet dev-certs https --trust` |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
