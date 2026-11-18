# AI Tuition Assistant (SE 4458 Midterm 1, Group 1)

[![CI](https://github.com/CaganKoyunseven/tuition-assistant-mcp/actions/workflows/ci.yml/badge.svg)](https://github.com/CaganKoyunseven/tuition-assistant-mcp/actions/workflows/ci.yml)

A conversational assistant that answers student questions about tuition deadlines, installments, payment rules, balances and payments. For each question an LLM (OpenAI function calling) decides whether to search the university policy document through an **MCP server**, call the **Tuition REST API** through a **Kong API Gateway**, or both.

> This README is being filled in phase by phase. Sections marked _TBD_ are not written yet.

## 1. Demo video
_TBD_

## 2. Architecture
_TBD_

## 3. Tech stack
_TBD_

## 4. Repository structure
_TBD_

## 5. Quick start (local, docker compose)
_TBD_

## 6. Deployed URLs (Azure)
_TBD_

## 7. API Gateway
_TBD_

## 8. Authentication and authorization
_TBD_

## 9. REST API

The Tuition API is a REST API (JSON, camelCase) versioned by URL segment (`/api/v1/...`). Interactive documentation: **Swagger UI at `/swagger`** (through the gateway). The exported OpenAPI document is in [`documentation/openapi/v1.json`](documentation/openapi/v1.json).

### Endpoints

| # | Operation | Method & route | Auth | Success | Errors |
|---|---|---|---|---|---|
| 1 | Get token | `POST /api/v1/auth/token` | public | 200 | 400, 401 |
| 2 | Query Tuition (Mobile) | `GET /api/v1/mobile/tuition/{studentNo}?term=` | none, **3 calls / student / day** | 200 | 400, 404, 429 |
| 3 | Query Tuition (Banking) | `GET /api/v1/banking/tuition/{studentNo}?term=` | JWT: `bank` (any student) or `student` (own only) | 200 | 400, 401, 403, 404 |
| 4 | Pay Tuition | `POST /api/v1/banking/payments` | JWT: `bank` or `student` (own only) | 201 | 400, 401, 403, 404, 409, 422 |
| 5 | Add Tuition | `POST /api/v1/admin/tuition` | JWT: `admin` | 201 / 200 | 400, 401, 403, 422 |
| 6 | Add Tuition – Batch | `POST /api/v1/admin/tuition/batch` (multipart, field `file`) | JWT: `admin` | 200 | 400, 401, 403, 413 |
| 7 | Unpaid Tuition Status | `GET /api/v1/admin/tuition/unpaid?term=&page=&pageSize=` | JWT: `admin` | 200 | 400, 401, 403 |

### Term vs. overall balance (Query Tuition)

`term` is an optional query parameter (`YYYY-FALL`, `YYYY-SPRING` or `YYYY-SUMMER`, case-insensitive):

- **with `term`** → `"scope": "TERM"`; the totals are for that term (404 `TUITION_NOT_FOUND` if the student has no record for it);
- **without `term`** → `"scope": "OVERALL"`; the totals are summed over all terms and `terms[]` lists every term in chronological order.

```json
{
  "studentNo": "20230001", "scope": "TERM", "term": "2026-FALL", "currency": "USD",
  "tuitionTotal": 6000.00, "paidAmount": 3000.00, "balance": 3000.00, "status": "PARTIALLY_PAID",
  "terms": [ { "term": "2026-FALL", "tuitionTotal": 6000.00, "paidAmount": 3000.00, "balance": 3000.00, "status": "PARTIALLY_PAID" } ]
}
```

The mobile and banking endpoints return the same shape. The mobile response also carries `X-Daily-Limit` and `X-Daily-Remaining` headers.

### Mobile query limit (3 per student per day)

The limit is keyed by the **student number in the path**, not by the caller's IP, and the window is the **UTC calendar day**. It is enforced in two layers:

1. **Kong gateway** (`rate-limiting` keyed by a header that a `pre-function` sets from the path): this is the primary enforcement.
2. **API database counter** (`daily_query_counters`, atomic `INSERT … ON CONFLICT DO UPDATE … RETURNING`): survives gateway restarts (Kong keeps its counters in memory).

The 4th call returns `429 RATE_LIMIT_EXCEEDED` with a `Retry-After` header (seconds until the next UTC midnight).

### Pay Tuition

```json
// request
{ "studentNo": "20230001", "term": "2026-FALL", "amount": 1000.00, "transactionId": "BANK-2026-000123" }
// 201 response
{
  "status": "SUCCESS", "paymentId": "…", "transactionId": "BANK-2026-000123",
  "studentNo": "20230001", "term": "2026-FALL", "currency": "USD",
  "paidAmount": 1000.00, "totalPaid": 4000.00, "remainingBalance": 2000.00,
  "recordStatus": "PARTIALLY_PAID", "channel": "BANK_APP", "recordedAt": "2026-11-20T10:15:00Z"
}
```

Rules:

- Partial and full payments are allowed. Payments are **simulated**: no card data is accepted or stored, and they post immediately.
- **Duplicate `transactionId` → `409 DUPLICATE_TRANSACTION`**, and the balance is not changed. The response includes the original payment, unless it belongs to a student the caller is not allowed to see. The database enforces this with a unique index, so it also holds under concurrent requests.
- **Amount greater than the outstanding balance → `422 AMOUNT_EXCEEDS_BALANCE`** (with `currentBalance`). Overpayment is not supported. A fully paid term → `422 ALREADY_PAID`.
- Amounts must be > 0, ≤ 1,000,000.00 and have at most 2 decimals. Inputs are never rounded; `10.001` → `400 INVALID_AMOUNT`.
- Concurrent payments cannot overdraw a balance: each tuition record has an optimistic concurrency token, and a conflicting payment is retried once with fresh data.
- `channel` is `BANK_APP` for the bank role, `AI_AGENT` when a student token sends `X-Client-Channel: ai-agent` (the AI agent), and `STUDENT` otherwise.

### Add Tuition: duplicate handling

> **Add Tuition is an idempotent upsert keyed by (studentNo, term).** Re-sending the same record is safe (`200 UNCHANGED`). Sending a different amount updates the assessed total (`200 UPDATED`), unless it would fall below the amount already paid, which is rejected with `422 AMOUNT_BELOW_PAID`. A new (studentNo, term) returns `201 CREATED`.

Unknown student numbers are created automatically (`"studentCreated": true`), because the specification has no "create student" operation and the batch CSV contains only the student number. Only `USD` is accepted (`400 UNSUPPORTED_CURRENCY`).

### Add Tuition – Batch (CSV)

Upload with `multipart/form-data`, field `file`, max 1 MB and 5,000 data rows. The header must be exactly `studentNo,term,amount,currency` (case-insensitive):

```csv
studentNo,term,amount,currency
20240001,2027-SPRING,5000.00,USD
20240002,2027-SPRING,5500.00,USD
20230004,2026-FALL,4800.00,USD
```

Each row is processed independently with the single Add Tuition rules (partial success). Rows are applied in file order, so a later row for the same (studentNo, term) updates an earlier one. The response reports the counts and per-line errors (the header is line 1):

```json
{
  "batchId": "…", "totalRows": 5, "succeeded": 1, "failed": 4,
  "created": 1, "updated": 0, "unchanged": 0, "studentsCreated": 1,
  "errors": [
    { "line": 3, "studentNo": "2024X004", "term": "2027-SPRING", "code": "INVALID_STUDENT_NO", "message": "…" },
    { "line": 4, "studentNo": "20240005", "term": "2027-SPRING", "code": "UNSUPPORTED_CURRENCY", "message": "…" },
    { "line": 5, "studentNo": "20230003", "term": "2026-FALL", "code": "AMOUNT_BELOW_PAID", "message": "…" },
    { "line": 6, "studentNo": "20240006", "term": "2027-WINTER", "code": "INVALID_TERM", "message": "…" }
  ]
}
```

A file whose rows all fail still returns `200`, because the batch itself was processed. A wrong header or a header-only file returns `400 INVALID_CSV`. Sample files: [`scripts/sample-data/tuition-batch.csv`](scripts/sample-data/tuition-batch.csv) and [`scripts/sample-data/tuition-batch-with-errors.csv`](scripts/sample-data/tuition-batch-with-errors.csv).

### Unpaid Tuition Status (paging)

`term` is required. `page` is ≥ 1 (default 1) and `pageSize` is 1–100 (default 20). Results are records with a balance > 0, ordered by balance (highest first), then by student number. A page beyond the last returns an empty `items` list.

```json
{
  "term": "2026-FALL", "page": 1, "pageSize": 20, "totalCount": 28, "totalPages": 2,
  "totalOutstanding": 77275.00, "currency": "USD",
  "items": [ { "studentNo": "20230004", "fullName": "Can Yilmaz", "tuitionTotal": 4500.00, "paidAmount": 0.00, "balance": 4500.00, "status": "UNPAID" } ]
}
```

### Errors

Every error the API produces is an **RFC 7807 ProblemDetails** body (`application/problem+json`) with two extensions: `code` (machine-readable) and `traceId`:

```json
{
  "type": "https://httpstatuses.io/422",
  "title": "Amount exceeds balance",
  "status": 422,
  "detail": "Requested amount 3500.00 exceeds the outstanding balance 3000.00 for term 2026-FALL.",
  "instance": "/api/v1/banking/payments",
  "code": "AMOUNT_EXCEEDS_BALANCE",
  "traceId": "00-…",
  "currentBalance": 3000.00
}
```

| HTTP | `code` | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | missing/malformed fields, invalid paging, unknown grant type |
| 400 | `INVALID_STUDENT_NO` / `INVALID_TERM` / `INVALID_AMOUNT` / `INVALID_TRANSACTION_ID` | format rules |
| 400 | `UNSUPPORTED_CURRENCY` | currency is not USD |
| 400 | `INVALID_CSV` | missing file, wrong header, no data rows, unreadable CSV |
| 401 | `UNAUTHORIZED` / `INVALID_CREDENTIALS` | missing or invalid token / bad login |
| 403 | `FORBIDDEN` / `FORBIDDEN_NOT_OWNER` | role not allowed / student accessing another student |
| 404 | `NOT_FOUND` / `STUDENT_NOT_FOUND` / `TUITION_NOT_FOUND` | unknown route / student / term record |
| 409 | `DUPLICATE_TRANSACTION` / `CONCURRENCY_CONFLICT` | reused transaction ID / concurrent update |
| 413 | `PAYLOAD_TOO_LARGE` | CSV file > 1 MB |
| 422 | `AMOUNT_EXCEEDS_BALANCE` / `ALREADY_PAID` / `AMOUNT_BELOW_PAID` | business rules |
| 429 | `RATE_LIMIT_EXCEEDED` | 4th mobile query for a student on the same UTC day |
| 500 | `INTERNAL_ERROR` | unexpected error (no stack trace in the body) |

### Versioning

Controllers declare `[ApiVersion("1.0")]` and routes use `api/v{version:apiVersion}/...`. Responses carry `api-supported-versions: 1.0`. A future `v2` would add new controllers and new gateway routes while `v1` stays unchanged. Unversioned routes (e.g. `/api/mobile/tuition/...`) return 404.

## 10. Database

**PostgreSQL 17** is the server database in every running environment (local docker compose and Azure Database for PostgreSQL Flexible Server). Data access uses EF Core with the Npgsql provider.

> SQLite appears **only inside the automated unit tests** as an in-memory test double, so `dotnet test` runs without Docker or cloud resources. It is never used by the running system.

![ER diagram](documentation/er-diagram.png)

Table descriptions, constraints and the Mermaid source: [`documentation/er-diagram.md`](documentation/er-diagram.md).

### Migrations

EF Core migrations live in `src/TuitionAssistant.Api/Infrastructure/Migrations/`. With `Database__MigrateOnStartup=true` the API applies them at startup (retrying while the database is still starting). To apply or inspect them manually:

```bash
dotnet tool restore
dotnet ef database update --project src/TuitionAssistant.Api --connection "<postgres connection string>"
dotnet ef migrations script --project src/TuitionAssistant.Api     # print the SQL
```

### Test data (seed)

With `Database__SeedOnStartup=true` an idempotent seeder creates the data below (each row is inserted only if it does not exist yet, so restarts never duplicate data).

| Student | Name (fictitious) | Tuition records | Used for |
|---|---|---|---|
| `20230001` | Elif Demir | 2026-FALL: total 6,000.00, paid 3,000.00 (`SEED-TXN-0001`, `SEED-TXN-0002`) → **balance 3,000.00** | Main demo student |
| `20230002` | Mert Kaya | 2026-FALL: 4,500.00 fully paid (`SEED-TXN-0003`); 2027-SPRING: 4,500.00 unpaid | Term vs. overall balance; ownership tests |
| `20230003` | Zeynep Aydin | 2026-FALL: total 6,000.00, paid 5,750.00 (`SEED-TXN-0004`) → **balance 250.00** | Below the $300 installment threshold (Policy §3.1) |
| `20230004` | Can Yilmaz | 2026-FALL: 4,500.00 unpaid | No scholarship may be inferred (Policy §5.2) |
| `20239001`–`20239025` | Paging Student 01–25 | 2026-FALL: 1,000.00 + i × 137.00, unpaid | Unpaid report paging |

Demo logins (override with `SEED__ADMINPASSWORD`, `SEED__STUDENTPASSWORD`, `SEED__BANKCLIENTSECRET`):

| Username | Role | Password / client secret |
|---|---|---|
| `admin` | admin | `Admin#2026` |
| `bank-app` | bank (client credentials) | `BankSecret#2026` |
| `20230001` … `20230004` | student | `Student#2026` |

These credentials are for the demo only. Passwords are stored as BCrypt hashes.

## 11. MCP server
_TBD_

## 12. AI Agent
_TBD_

## 13. Testing

```bash
dotnet test
```

Tests run with the .NET 10 SDK only: no Docker, no cloud and no OpenAI key are required.

## 14. Cloud deployment
_TBD_

## 15. LLM cost and low-cost approach
_TBD_

## 16. Assumptions
_TBD_

## 17. Reproduce steps for the instructor
_TBD_

## 18. Known limitations
_TBD_

## 19. Evidence index
_TBD_
