# EasyCob

Especificação funcional e técnica: [`docs/specs/README.md`](docs/specs/README.md).

Backend do MVP de gestão de cobranças: monólito modular em .NET 10, PostgreSQL, SQS e WhatsApp Cloud API.

## Executar a base local

```powershell
wsl -d Ubuntu --cd "$PWD" -- docker compose up -d --build
```

O Compose inicia PostgreSQL, aplica as migrations e sobe API e frontend. Acesse `http://localhost:3001`, `http://localhost:8080/swagger` e `http://localhost:8080/health/ready`.

Configure Cognito por variáveis de ambiente conforme `.env.example` antes de testar o login. Para encerrar:

```powershell
wsl -d Ubuntu --cd "$PWD" -- docker compose down
```

Após a primeira migration, cadastre o tenant e use o mesmo UUID na claim Cognito `tenant_id`:

```sql
INSERT INTO tenants (id, name, time_zone, currency, created_at)
VALUES ('UUID_DO_TENANT', 'Empresa', 'America/Sao_Paulo', 'BRL', now());
```

Para executar também o worker, configure SQS/WhatsApp e rode `dotnet run --project apps/worker/EasyCob.Worker.csproj`.

## Verificação

```powershell
dotnet build
dotnet test
```

## Homologação e pipelines

O ambiente de homologação (compose num EC2 atrás de CloudFront, deploy via GitHub Actions) e o passo a passo completo estão em [`docs/homologacao.md`](docs/homologacao.md). Decisão de arquitetura em [`docs/adr/0004-deploy-homologacao.md`](docs/adr/0004-deploy-homologacao.md).

## Limites atuais

- `EasyCob.Core` contém os módulos Tenancy, Customers, Billing, Messaging, Finance e Audit.
- A API resolve `tenant_id` exclusivamente da claim JWT e aplica filtros globais no EF Core.
- O PostgreSQL reforça o isolamento com RLS e chaves estrangeiras compostas por `tenant_id`.
- Cobranças gravam o evento de criação na outbox na mesma unidade de trabalho.
- O worker publica a outbox no SQS com bloqueio `SKIP LOCKED` e entrega pelo menos uma vez.

## API do MVP

- `/customers`: cadastro, contatos, consentimento, CSV, exportação e anonimização.
- `/charges`: títulos, parcelas, pagamentos e cancelamento.
- `/message-templates`, `/collection-rules`, `/messages`: régua e operação WhatsApp.
- `/webhooks/whatsapp`: verificação, assinatura, deduplicação, status e opt-out.
- `/finance`: resumo e projeção diária.
- `/tenant`, `/audit-events`: configuração, usuários e trilha imutável.

Todas as rotas de negócio exigem JWT; o `tenant_id` vem apenas da claim. Papéis: `Owner`, `Admin`, `Finance`, `Collector` e `Viewer`.
O contrato OpenAPI fica disponível em `/swagger/v1/swagger.json`; a interface Swagger UI é habilitada em desenvolvimento.
