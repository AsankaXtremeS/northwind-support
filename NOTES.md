## Task 1 commits
1. `fix(web): DEFECT-117 every filter change now sends a request`
2. `feat(api): apply ticket list filters in SQL before counting and paging`
3. `test(api): cover ticket list filtering against SQLite in-memory`

---

# Task 1.1 – DEFECT-117

### Reproduction (Chrome DevTools → Network, on page 1)
1. Change Status to "In progress" → **no** request to GET /api/tickets.
2. Type in search → a request goes out **with** `status=InProgress`.
3. Clear search → change Status again → no request again.
4. On page 2, changing a filter **does** send a request (updateFilters resets page to 1,
   and page was a dependency) → explains "sometimes" in the defect title.

### Root cause
File: `apps/web/src/features/tickets/hooks/useTicketList.ts` (fetch effect, old lines 45–50)

The effect that loads tickets only listed `search`, `page` and `pageSize` as dependencies, and an
`eslint-disable` comment hid the missing ones. So changing status/priority/category/customer/
agent/unassigned/sort on page 1 updated state but never re-ran the request. The next search
change re-ran it and read the latest state, which is why the earlier status suddenly appeared.

Same cause also broke: sort changes and the "Clear filters" button on page 1.

Why it got in: the react-hooks/exhaustive-deps lint rule warned about it, but it was switched off.

### Regression tests
File: `apps/web/src/features/tickets/hooks/useTicketList.test.ts`
- Run the real `ticketsApi.getTickets` with a mocked `fetch`, and assert on the URL actually sent.
- Dropdown/sort/reset tests assert **immediately** (flush only, no debounce wait).
- Search test asserts one request only, and nothing before `DEBOUNCE_MS` has passed.

#### Result on ORIGINAL code (first version) – 17:51
```
 ❯ useTicketList.test.ts (9 tests | 7 failed)
     ✓ searches with the text the user typed, once the typing settles
     × sends a new request with the status when the status filter changes on page 1
     × sends a new request with priority / categoryId / customerId / assignedAgentId / unassignedOnly
     ✓ sends only one request after the debounce when search is typed quickly
     × sends a new request without the cleared filter when filters are reset on page 1
 ✓ TicketTable.test.tsx (1 test)

AssertionError: expected "vi.fn()" to be called 2 times, but got 1 times
 Tests  7 failed | 3 passed (10)
```

#### Result on ORIGINAL code after tightening – 17:58
```
 ❯ useTicketList.test.ts (11 tests | 9 failed)
     ✓ searches with the text the user typed, once the typing settles
     × immediately sends a new request with status / priority / categoryId / customerId /
       assignedAgentId / unassignedOnly / sortBy / sortDirection ... on page 1
     ✓ sends only one request, and only once the debounce has passed, when search is typed quickly
     × immediately sends a new request without the cleared filter when filters are reset on page 1
 ✓ TicketTable.test.tsx (1 test)

AssertionError: expected "vi.fn()" to be called 2 times, but got 1 times
 Tests  9 failed | 3 passed (12)
```
Why this proves the bug: only the initial page-load request was sent; changing a filter on page 1
sent nothing. The search tests pass because search WAS already a dependency.

#### Result after fix
- `npm run test --workspace apps/web`: 12/12 pass. `npm run lint` and `npm run typecheck` clean.

### Fix (how it works)
- `filters` = what the user selected/typed. `settledSearch` = the search text after typing stopped.
- Fetch effect depends on `[filters, settledSearch]`. It skips while the user is still typing
  (`filters.search !== settledSearch`), so no request per keystroke.
- A second effect copies `filters.search` into `settledSearch` after `DEBOUNCE_MS`.
- Any non-search change (dropdown, sort, reset) settles the search at once, so it fetches
  immediately and includes any half-typed search.
- `AbortController` cancels the previous request; aborted responses are ignored (no error shown).
- `eslint-disable` removed; exhaustive-deps passes.
- Task 2's `slaStatus` will work with no extra wiring: it is just one more field in `filters`.

### Decisions
- Request cancellation: added optional `signal` to `ticketsApi.getTickets`, passed through to
  `fetch`, so an outdated request is actually cancelled (not just ignored).
  Rejected: only ignoring stale responses (network request would keep running).
  Required updating ONE existing assertion (it expected exactly one argument); the behaviour it
  checks is unchanged.
- Tests and fix committed together, so history never contains failing tests.

### Manual verification after fix (page 1, Network tab)
- [x] Each dropdown → one request immediately, with the right value
- [x] Typing → one request after typing stops
- [x] Clear filters → request
- [x] Sort → request
- [ ] Type "inv" then quickly change Status → one request with both search and status
- [ ] Changing filters fast → older requests show as (canceled)

---

# Task 1.2 – Server-side filtering

### What was built
- `TicketQueries.cs`: private static `ApplyFilters` with one condition per filter, called before
  `CountAsync`. Remarks updated.
- `TicketQuery.cs`: comments only (AND behaviour, unassigned + agent case, partial reference match).
- `TicketBuilder.cs`: added `WithTitle`, `ForCustomer`, `InCategory` (defaults unchanged, so
  existing tests are unaffected).
- `TicketQueriesFilteringTests.cs` (new): 11 test methods / 15 cases.

### Decisions
- Filters applied to the joined entities (`TicketWithLabels`: Ticket + Customer + Category + Agent)
  BEFORE the DTO projection, so every condition is a SQL WHERE on real columns and existing
  indexes (IX_Tickets_Status_Priority, IX_Tickets_CustomerId, IX_Tickets_CategoryId,
  IX_Tickets_AssignedAgentId) can be used. Filtering after projection was rejected: the DTO
  contains C# (`SlaEvaluator`) that may not translate to SQL.
- `ApplyFilters` is a private static method next to `ApplySort` (`TicketWithLabels` is a private
  nested class, so it can't be an extension method). One condition per filter → `slaStatus` is one line.
- Order: `TicketsWithLabels → ApplyFilters → CountAsync → ApplySort → Skip/Take → Select`.
- Reference (TCK-0042) is a real column (nvarchar(20), unique index), generated on create.
  Search uses `Contains` → `LIKE '%term%'`. "0042" also matches TCK-10042 (accepted).
- Search is trimmed; blank/whitespace is ignored.
- Search is case-insensitive on SQL Server (default collation). EF escapes `%` and `_`.
- Leading-wildcard LIKE cannot use an index seek → table scan. Fine at this size;
  at 10M tickets I would use full-text search.
- `UnassignedOnly=true` + `AssignedAgentId` → ANDed → empty page, totalCount 0 (no special case).
- SQL checked via existing EF logging (`Microsoft.EntityFrameworkCore.Database.Command: Information`).
  Sensitive data logging deliberately NOT enabled, to avoid logging parameter values.
- Tests: SQLite in-memory (real SQL, no external DB, `EnsureCreated`, kept-open connection,
  6 seeded tickets, tested through public `GetPagedAsync`).
  Rejected EF InMemory provider (not SQL, different Contains behaviour).

### SQL check (SQL Server: GET /api/tickets?priority=High&search=contoso → totalCount 2)
```sql
-- COUNT query
SELECT COUNT(*)
FROM [Tickets] AS [t]
INNER JOIN [Customers] AS [c] ON [t].[CustomerId] = [c].[Id]
INNER JOIN [Categories] AS [c0] ON [t].[CategoryId] = [c0].[Id]
LEFT JOIN [Agents] AS [a] ON [t].[AssignedAgentId] = [a].[Id]
WHERE ([t].[Title] LIKE @search_contains ESCAPE N'\'
   OR [t].[Reference] LIKE @search_contains0 ESCAPE N'\'
   OR [c].[Name] LIKE @search_contains1 ESCAPE N'\')
  AND [t].[Priority] = @priority

-- Page query: same WHERE, then
ORDER BY [t].[CreatedAtUtc] DESC, [t].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p6 ROWS ONLY
```

### Case sensitivity
| Search | SQL Server result |
| --- | --- |
| Contoso | 7 tickets |
| contoso | same 7 tickets |
| TCK-0001 | TCK-0001 |
| tck-0001 | TCK-0001 |

- SQLite translates `Contains` to `instr(col, @search) > 0`, which is case-sensitive → tests use
  matching case. Known limitation: tests check filter logic, not collation; collation verified
  manually on SQL Server.

### Test result
- `dotnet test`: 40 unit + 4 architecture = 44 pass (baseline 30). No existing test removed
  (checked `git diff apps/api/tests`: only TicketBuilder gained optional methods).

### Manual verification (website)
- [ ] High → only High, count < 40
- [ ] Low → not all 40
- [ ] Status, category, customer, agent, unassigned each narrow the list, alone and combined
- [ ] Search: `contoso`, `tck-0001`, part of a title, `  contoso  `
- [ ] Pager page count correct

### How to add the next filter
Add one line in `ApplyFilters`: `if (query.X is { } x) rows = rows.Where(...);`

---

# AI usage log
**Tool:** Claude Code (in the repo) for investigation, tests and implementation; Claude chat for
planning, reviewing diffs and explaining concepts.

**Workflow that worked**
- Session rules first: no git commands, no unrelated changes, no `any`/`@ts-ignore`/eslint-disable.
- Test-first: asked for regression tests only, confirmed they FAIL on the original code, then
  asked for the fix.
- Asked questions before code for the backend (where to filter, Reference column, edge case,
  logging, files to change) and approved the answers.
- Reviewed every `git diff` myself and ran tests/lint/typecheck myself before committing.

**Accepted as-is**
- Claude Code's investigation correctly located the defect in `useTicketList.ts` and the missing
  filtering in `TicketQueries.cs`. Verified by reading the code and reproducing in the browser.

**Changed or rejected**
- REJECTED: `JSON.stringify(filters)` in the dependency list. It keeps the eslint-disable and still
  debounces every filter, so dropdowns would not fire immediately.
- CHANGED: first AI tests waited 300 ms before asserting, so a "debounce everything" fix would
  pass. Tightened them to assert immediately, use `DEBOUNCE_MS`, and cover sort.
- REVERTED: Claude edited `.gitignore` and rewrote `package-lock.json` (unrelated). Reverted so the
  defect commit only contains the fix and its tests. Used `.git/info/exclude` for NOTES.md instead.
- CORRECTED: Claude said SQLite would be a new package; the assignment says it's already
  referenced. Asked it to check instead of adding a dependency (it was already there).
- Claude stopped and asked before editing an existing test (request cancellation needed a signature
  change). I chose real cancellation and approved one assertion change.

**How generated code was verified**
- Regression tests confirmed failing before the fix and passing after.
- Read the generated SQL in the API log (WHERE in both COUNT and page queries).
- Manual browser checks in the Network tab and on the list.

---

# For README / APPROACH.md later
- Defect: root cause, fix, how to verify (reproduction steps above + `npm run test --workspace apps/web`).
- Filtering: where applied, how SQL was checked, how to add the next filter.
- Known limitations so far:
  - Search uses leading-wildcard LIKE → scan; full-text search for large data.
  - SQLite tests don't check SQL Server collation (verified manually).
  - `System.Security.Cryptography.Pkcs` 9.0.11 vulnerability warning (pre-existing).



# Task 2 – Escalation & Assignment

## Findings from investigation
- Sla config existed only in appsettings.Development.json and was never read.
- SlaEvaluator never returned AtRisk (no window start/threshold input).
- SLA status computed in C# projection → could not be filtered in SQL.
- No domain methods to set priority/due date; triage and escalation missing.
- Manual AssignTicketCommandHandler skips specialist rule (manual override).
- Create does not check Category.IsActive.
- Seeder comment points to non-existent database/02_seed.sql (real: db/seed.sql).
- Test naming mixed after Task 1 (snake_case vs repo's Method_Condition_Outcome).

## Decisions (D1–D14)

## Task 2 design (approved)

Approved with changes. Points marked **(changed on approval)** differ from the first proposal.

### 1. Classes, where they live, how create and escalate share them

**Domain** (pure, no framework → satisfies ArchitectureTests). Small rule units, unit-tested directly, no interfaces:

| Rule | Class | Signature |
|---|---|---|
| BR-1 | `PriorityPolicy` (static) | `Decide(TicketPriority? requested, Category category) → PriorityDecision(Priority, Reason)` |
| BR-2/3/6 | `SlaWindowPolicy` | built from `SlaTerms` record (base minutes per priority, premium multiplier, floor). `Calculate(priority, tier) → SlaWindow(Minutes, Reason)` |
| BR-4/5, D5, D7 | `AgentSelectionPolicy` (static) | `Choose(IReadOnlyList<AgentCandidate>, int? currentAgentId) → AssignmentDecision(AgentId?, Reason)` |
| BR-8 | `SlaStatusRule` | see section 2 |

- `AgentCandidate(Id, FullName, IsActive, MaxOpenTickets, OpenTicketCount, MeetsSpecialistRequirement)` read model. **(changed on approval)** `MeetsSpecialistRequirement` = the category does not require a specialist OR the agent has that specialization. A General ticket is assignable to any active agent below their limit.
- `TriageDecision` (priority, window minutes, due date, agent, reasons) lives in **Domain**, because a Domain method takes it. **(changed on approval)**

**Application:**
- `SlaOptions`: bound `Sla` section, `IValidateOptions` + `ValidateOnStart`. A factory maps it to Domain `SlaTerms`, so Domain never sees Options.
- `TicketTriage` (concrete, no interface): `DecideAsync(priority, category, customer, currentAgentId?, excludeTicketId?, now, ct)` runs SlaWindowPolicy + AgentSelectionPolicy → `TriageDecision` with three reasons.
  - Create: `PriorityPolicy.Decide` → `TicketTriage`.
  - Escalate: `ticket.NextPriority()` → the same `TicketTriage`.
- Handlers: `RaiseTicketCommandHandler` (extended), `EscalateTicketCommandHandler` + `EscalateTicketRequestValidator` (reason 5–500 after trim, escalatedBy required ≤ 100), `GetTicketEscalationsQueryHandler`.

**Interfaces only at database seams:**
- `ICategoryRepository.GetByIdAsync`, `ICustomerRepository.GetByIdAsync` (triage needs the flags and the tier).
- `IAgentRepository.GetCandidatesAsync(categoryId, excludeTicketId, ct)`: D10, one SQL statement (specialist check as a scalar `Any()`, open count as a correlated `COUNT`, no N+1). Excluding the escalated ticket from counts is D6, in one place.
- `ITicketQueries.GetEscalationsAsync`.
- The query only loads data; every eligibility decision stays in `AgentSelectionPolicy`, so rules are not split between SQL and C#.

### 2. D2 – SLA status in C# and SQL without duplication → Option A chosen

**Option A (chosen):** one expression.
- `SlaStatusRule.For(DateTime nowUtc, int atRiskPercent) → Expression<Func<Ticket, SlaStatus>>` in Domain (`System.Linq.Expressions` is BCL).
- SQL: list, detail and customer projections, and the one new `slaStatus` line in `ApplyFilters`, via a ~15-line parameter-rebinding helper in Infrastructure (no new package).
- C#: tests use `.Compile()`. Create/escalate responses are re-read through `GetTicketQueryHandler`, so they get the SQL result.
- `SlaEvaluator.Evaluate` is removed; its tests move onto the compiled rule.
- Pros: single source of truth, SQL and C# cannot drift. Cons: harder to read, compile cost (tests only).

**Option B (rejected):** keep C# `SlaEvaluator` + a separate SQL expression, kept in step by one shared boundary test suite run against both. Simpler C#, but the rule exists twice; the assignment marks down duplicated rules.

**Arithmetic chosen to translate on both providers:** all date arithmetic is on the `now` parameter, never on a column (avoids the UTC value converter blocking translation):

```csharp
t.DueAtUtc <= nowUtc.AddSeconds(t.SlaWindowMinutes!.Value * 60 * atRiskPercent / 100)
```

- Integer arithmetic; C#, SQL Server and SQLite all truncate integer division the same way, so compiled C# and SQL agree exactly.
- Expected: SQL Server `DATEADD(second, …, @now)`; SQLite `strftime(..., @now, … || ' seconds')` in EF's stored date format.
- **Not yet verified.** Verified in step 3; if it does not translate on SQL Server or SQLite → stop and decide before continuing. **(changed on approval)**

### 3. New Ticket domain methods

- **`Raise(...)` signature unchanged** (TicketBuilder and existing tests use it). **(changed on approval)**
- **`ApplyTriage(TriageDecision triage, DateTime now)`**, called right after `Raise`: sets priority, agent, `SlaWindowMinutes`, `DueAtUtc = now + window`. **(changed on approval)**
- `bool CanEscalate` = open and not Critical; goes on the detail DTO so the UI can disable the control.
- `EnsureCanEscalate()`: throws `BusinessRuleViolationException` (409) for Resolved/Closed or Critical, message says which. Called by the handler **before** triage (Critical has no next priority).
- `TicketPriority NextPriority()`: current + 1.
- `Escalate(TriageDecision triage, string reason, string escalatedBy, DateTime now) → TicketEscalation`:
  - re-checks `EnsureCanEscalate`;
  - guards `triage.Priority == NextPriority()`;
  - guards reason/actor with `ArgumentException` (defence in depth; the validator gives the 400);
  - sets priority, agent, window, due date, `UpdatedAtUtc`;
  - appends an immutable `TicketEscalation` to a private `_escalations` collection (EF inserts it without loading existing history).

### 4. TicketEscalations entity and configuration

| Column | Type | Notes |
|---|---|---|
| Id | int identity | primary key |
| TicketId | int | FK → Tickets, **Restrict** (D9) |
| FromPriority, ToPriority | int | check constraint 1–4 |
| FromAgentId, ToAgentId | int, nullable | FK → Agents, Restrict |
| FromDueAtUtc | datetime2(3), nullable | older tickets may have no due date |
| ToDueAtUtc | datetime2(3), not null | an escalation always sets one |
| Reason | nvarchar(500), required | |
| EscalatedBy | nvarchar(100), required | |
| EscalatedAtUtc | datetime2(3), required | |

- Index `IX_TicketEscalations_TicketId_EscalatedAtUtc` on `(TicketId, EscalatedAtUtc DESC)`; also serves the TicketId FK, so EF creates no separate one.
- New `Tickets.SlaWindowMinutes int NULL`, check `> 0`.
- Migration backfill: `UPDATE Tickets SET SlaWindowMinutes = DATEDIFF(minute, CreatedAtUtc, DueAtUtc) WHERE DueAtUtc IS NOT NULL`.
- Seeder sets `SlaWindowMinutes`; `db/schema.sql` and `db/seed.sql` updated too.

### 5. API shapes (wrapper responses approved)

**`POST /api/tickets`**: same request body; 201 with `Location`:

```json
{ "ticket": { ...TicketDetail, "canEscalate": true },
  "triage": {
    "priority": "Critical", "priorityReason": "Security always forces Critical (requested Low).",
    "slaWindowMinutes": 120, "slaWindowReason": "Critical 4 h, halved for a Premium customer: 2 h.",
    "assignedAgent": { "id": 3, "fullName": "Priya Nair" } | null,
    "assignmentReason": "Fewest open tickets (4) among 2 eligible Security specialists." |
                        "Unassigned: no active Security specialist is below their open-ticket limit." } }
```

Category names in reasons come from data, never hard-coded.

- **Inactive category on create → 409** with a clear message. **(changed on approval)**

**`POST /api/tickets/{id}/escalate`**: body `{ "reason": "...", "escalatedBy": "..." }`.
- 200 `{ "ticket": TicketDetail, "escalation": TicketEscalation }`
- 400 invalid reason / missing actor (validator), 404 unknown ticket, 409 "already Critical" or "Resolved/Closed".

**`GET /api/tickets/{id}/escalations`**: 200 array newest first, or 404 for an unknown ticket.
- Item: `{ id, fromPriority, toPriority, fromAgent: AgentSummary|null, toAgent: AgentSummary|null, fromDueAtUtc, toDueAtUtc, reason, escalatedBy, escalatedAtUtc }`; agent names from two left joins in one query.

**`GET /api/tickets?slaStatus=AtRisk`**: one property on `TicketQuery`, one line in `ApplyFilters`.

### 6. Design patterns

1. **Policy objects** (Strategy-like): BR-1, BR-2/3/6, BR-4/5 each a small pure class shared by create and escalate. No interfaces, because nothing swaps them out (say so honestly in the README).
2. **Options pattern with startup validation**: BR-6 values change through config; bad config stops the app at startup instead of producing wrong due dates.
3. **Specification as an expression**: BR-8 is one expression that runs as SQL (filter + projection) and compiles to C# for tests.

Existing Repository, Unit of Work and one-handler-per-use-case structure unchanged.

### 7. Risks and resolutions

- **BR-8 boundaries (confirmed):** unresolved exactly at due = **AtRisk** (Breached is strictly past due); exactly 25% of window left = **AtRisk**; resolved exactly at due = **Met**.
- **Response shape (confirmed):** wrappers `{ ticket, triage }` and `{ ticket, escalation }`; the frontend `createTicket` changes accordingly.
- **D3 validation:** all four priorities required in `BaseWindowHours`, windows > 0, `0 < multiplier ≤ 1`, floor > 0, threshold 0–100. Windows rounded to whole minutes before the floor is applied.
- **D6:** applied by excluding the escalated ticket from every count; harmless for other agents.
- **D9:** Restrict means tickets/agents with history cannot be deleted. Fine: agents are deactivated, tickets are not deleted. `DESC` in the index is optional (SQL Server can scan backwards) but harmless.
- **Concurrency → Known limitations (confirmed):** two simultaneous escalations could both apply; two creates could both pick an agent with one free slot. Fix would be a `rowversion` token on Ticket.
- **Inactive category (decided):** rejected on create with 409.

### Implementation steps (one at a time; after each: dotnet test, web tests/lint/typecheck if web changed, list files, stop)

0. Rename Task 1 filtering tests to `Method_Condition_Outcome` PascalCase. **Done** (44/44 tests pass).
1. Bind `Sla` to `SlaOptions` with `IValidateOptions` + `ValidateOnStart`; move the section into appsettings.json (Development still works); validator tests.
2. `Tickets.SlaWindowMinutes` (int NULL, check > 0) + migration with backfill; update seeder, `db/schema.sql`, `db/seed.sql`; PersistenceModelTests must pass.
3. `SlaStatusRule` as one expression in list/detail/customer projections; remove `SlaEvaluator`, move its tests onto the rule; boundary tests (compiled C# AND SQLite through the query); show the SQL Server SQL from the log.
4+. Given one by one.



## Step 2 – SlaWindowMinutes
- Stored the applied window (not the status) so AtRisk = "25% or less of THIS window left",
  and escalation can restart it. SLA status itself is still never stored (BR-8).
- Migration order: add column → backfill (DATEDIFF minute) → check constraint (> 0).
  Rows with due <= created stay NULL instead of failing.
- Backfill wrapped in EXEC so the generated script also runs as a single batch (relevant for
  production migration via script/bundle).
- Verified on the existing seeded DB: migration applied, 0 tickets with a due date but no window.

## Step 3 – SLA status rule
- One Expression<Func<Ticket, SlaStatus>> (SlaStatusRule) → used in SQL projections (and the
  slaStatus filter in step 7) and compiled for unit tests. Removed SlaEvaluator (rule existed in C# only,
  never returned AtRisk). Rejected option B (C# + separate SQL copy) because it duplicates the rule.
- Date arithmetic on the @now parameter only, never on columns → translates on both providers.
- Boundaries tested on compiled C# AND SQLite: at due (unresolved) = AtRisk; exactly 25% left = AtRisk;
  resolved exactly at due = Met.
- Verified SQL Server SQL in the API log (DATEADD on @now inside CASE).

## Step 4 – Policies
- PriorityPolicy, SlaWindowPolicy, AgentSelectionPolicy in Domain. Pure, no interfaces (nothing swaps them).
- Window rounded to whole minutes, then the 1 h floor. Floor applies to every tier.
- Agent: keep current if still eligible, else fewest open, tie → lowest Id, none → unassigned + reason.
- Multiplier formatted with InvariantCulture in the reason text (could print "0,5" on some machines).

## Step 5 – Triage on create
- TicketTriage (Application) used by create AND escalate, so the rules run in one place.
- POST /api/tickets → 201 { ticket, triage } with a reason for priority, window and agent.
- No eligible agent → still 201, unassigned, with the reason. Inactive category → 409.
- Candidates loaded in ONE query (checked in a SQLite test by counting commands).

## Step 6 – Escalation
- Ticket.Escalate checks the rules again and returns an immutable TicketEscalation row.
- Due date restarts from the moment of escalation. Escalated ticket left out of agent counts,
  so the current agent isn't blocked by their own ticket.
- 400 (validator, Cascade.Stop so null doesn't crash), 404, 409 with "already Critical" or "is Resolved".
- History newest first, ties broken by Id. Index (TicketId, EscalatedAtUtc DESC) serves both the FK
  and this query.

## Step 7 – slaStatus filter
- One line in ApplyFilters, same SlaStatusRule as the projection, same "now" for both
  → the filter and the shown status can't disagree. Applied before count and paging.

## Step 8 – SLA indicator + filter (web)
- One SlaIndicator for list and detail. Word + symbol per state, colour is extra (not colour only).
- States in a Record<SlaStatus, ...> → a new API state is a compile error, not a grey badge.
- SLA dropdown just adds slaStatus to filters; the DEFECT-117 fix handles the rest (no extra wiring).

## Step 9 – Escalation panel + history (web)
- Client validation = server limits (5–500 trimmed, name ≤ 100). Invalid input never reaches the API.
- Panel hidden with a "why not" text when canEscalate is false; 409 from the server still shown.
- Ticket and history updated from the response, no reload.
- escalatedBy is typed by hand until Task 3 (then it should come from the logged-in user).

## Step 10 – Triage result after create (web)
- Confirmation shows priority, SLA window + due date, agent or "Unassigned", each with its reason.

## Verification (SQL Server, after step 10)
- AddTicketEscalations migration applied on API start.
- TCK-0023 Medium → High (due +8 h, unassigned → Sara) → Critical (due +4 h, Sara kept).
- Critical → 409, Resolved → 409, short reason → 400, empty actor → 400, unknown id → 404.
- Rows and DESC index checked in the DB. Error keys (Reason, EscalatedBy) match what the UI reads.
- Tests: dotnet 191 + 4, web 42. Lint and typecheck clean.
- [ ] Browser check: escalate twice, raise a Security ticket for a Premium customer.

## AI usage (Task 2)
- Same workflow: design approved first, written to TASK2_PLAN.md, one step per commit, I review diffs.
- Bugs AI introduced, caught by tests: static field order (compiled rule declared before the
  expression → null); rounding test with 0.725 (floating point); handler test used a mocked Id of 0.
- Lint caught constants exported from a component file → moved to slaStates.ts.
- CHANGED: 409 test only checked the message; spec says "ticket unchanged" → added that assert.
- Did NOT let it run the API against the dev DB without asking (migration writes data).

## README todo (Task 2)
- Why Sla moved to appsettings.json (BR-6 "or justify"): config for all envs, validated on start.
- Index (TicketId, EscalatedAtUtc DESC) and why.
- Known limitations: no rowversion → two escalations at once can both apply; manual assign skips
  the specialist rule; SLA config change doesn't re-window existing tickets.
