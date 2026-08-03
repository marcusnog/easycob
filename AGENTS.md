# Repository Guidelines

EasyCob: modular monolith — ASP.NET Core 10 (api + worker), Next.js 16 (web), PostgreSQL, SQS, WhatsApp Cloud API. Functional/technical spec: `docs/specs/README.md`; architecture decisions: `docs/adr/`. Docs and code comments are in pt-BR; match that.

## Layout

- `apps/api/EasyCob.Core/` — business modules under `Modules/{Tenancy,Customers,Billing,Messaging,Finance,Audit}` (one `Entities.cs` per module plus static helpers like `InstallmentSchedule`), `Data/` (DbContext, EF migrations), `Tenancy/Tenancy.cs` (`TenantContext`). Architecture tests enforce that modules do not reference each other.
- `apps/api/` — minimal-API endpoints in `Endpoints/`, thin over Core; one flat file per module (plus `WhatsAppWebhookEndpoints.cs`).
- `apps/worker/` — hosted services (outbox publisher, SQS consumer, WhatsApp dispatcher, overdue updater).
- `infra/` — OpenTofu AWS provisioning (Cognito incl. the pre-token Lambda that injects `tenant_id`/`cognito:groups` claims, Postgres `init.sql`). `*.tfstate` is gitignored; never commit it.
- `apps/web/` — Next.js App Router. **Read `apps/web/AGENTS.md` first: it is Next.js 16 with breaking changes; consult `node_modules/next/dist/docs/` before writing code.**
- `tests/{architecture,integration}/` — xUnit. New .NET projects must be added to `EasyCob.slnx` (new XML solution format, not `.sln`).

## Commands (from repo root)

```powershell
dotnet build                                     # api, worker, core, tests
dotnet test                                      # both suites; runs offline, no Docker needed
dotnet test tests/integration/EasyCob.IntegrationTests.csproj
dotnet test tests/integration/EasyCob.IntegrationTests.csproj --filter FullyQualifiedName~TenantIsolationTests
dotnet format                                    # formatter gate
dotnet tool restore                              # installs dotnet-ef (pinned in dotnet-tools.json)
dotnet tool run dotnet-ef migrations add <Name> --project apps/api/EasyCob.Core
npm --prefix apps/web ci
npm --prefix apps/web run lint
npm --prefix apps/web run typecheck
npm --prefix apps/web run dev
```

- Local dev URLs: web `http://localhost:3001`, api `http://localhost:8080` (`/swagger` and `/health/ready` in Development) — not the framework defaults 3000/5000.
- `Directory.Build.props` sets `TreatWarningsAsErrors=true` — a single warning fails the build.
- Integration tests swap EF for InMemory and auth for a test handler keyed off `X-Tenant-Id`/`X-Role` headers (`tests/integration/BackendApiTests.cs`); no Postgres/SQS/WhatsApp required.
- The web app has **no test runner** (`dev`, `build`, `start`, `lint`, `typecheck` only).
- EF migrations have a design-time factory in Core (`EASYCOB_POSTGRES` env, else localhost `postgres`/`postgres`), so `--startup-project` is not needed.
- Docker on this Windows host runs through WSL: `wsl -d Ubuntu --cd "$PWD" -- docker compose up -d --build`. The `worker` service sits behind the `worker` compose profile and needs SQS/WhatsApp env vars; api + web run with the `compose.yaml` defaults (ports 8080/3001). The api image bakes an EF migration bundle executed by the `migrate` service. To run the worker outside Docker: `dotnet run --project apps/worker/EasyCob.Worker.csproj`.

## Tenancy & security

- `tenant_id` is resolved only from the authenticated JWT claim (`apps/api/Program.cs`), pushed to Postgres via `set_config('easycob.tenant_id', ...)` inside a request-scoped transaction; RLS is the second barrier. Never accept it from request input.
- Roles resolve from the `cognito:groups` claim (Cognito group names `Owner`/`Admin`/`Finance`/`Collector`/`Viewer`); missing or unknown group falls back to `Viewer`. Raw Cognito claims (`sub`, `email`, `tenant_id`, `cognito:groups`) are used as-is because `MapInboundClaims = false`.
- JWT check in `Program.cs`: `ValidateAudience = false`; a token is rejected unless `token_use == "access"` and `client_id` matches `Authentication:Audience`. Don't "fix" the audience validation to the standard scheme.
- Every business entity is `ITenantEntity`; `EasyCobDbContext` adds global query filters, composite `(TenantId, Id)` keys/FKs, and throws on `SaveChanges` without a resolved tenant.
- API and worker fail fast at startup when required config is missing or still contains `CHANGE_ME` — but only in Production (`IsProduction()` gate); `dotnet run` in Development starts regardless. `.env` is gitignored; only edit `.env.example`.

## Conventions

- 4-space C# / 2-space TypeScript/JSON; `PascalCase` C# public members, `camelCase` locals/TS, `kebab-case` route prefixes (`/message-templates`, `/collection-rules`). Endpoints are flat files in `Endpoints/`, one per module.
- Tests named `Method_Scenario_Result`; money rules, tenant isolation, and webhook signatures/idempotency (incl. inbox dedup) already have coverage — extend it. The worker (outbox→SQS, WhatsApp dispatch) has **no** test coverage. Bug fixes include one regression test.
- Conventional Commits, e.g. `feat(billing): add installment schedule`; record consequential architecture changes in `docs/adr/`. PRs note behavior change, testing, migration/rollback impact.
