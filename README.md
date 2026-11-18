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
_TBD_

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
