# Homologação — setup e operação

Ambiente: `easycob-homol` — compose num EC2 atrás de CloudFront (ver ADR 0004).
Stack: OpenTofu (Cognito, SQS, S3, ECR, EC2, CloudFront, budget, shutdown) + GitHub Actions (CI/CD).

## 1. Pré-requisitos

- OpenTofu, credenciais AWS com permissão para criar os recursos e o GitHub.
- `budget_notify_email` para o alerta de custo (obrigatório no ambiente homol).

## 2. Infra (dois applies — o domínio CloudFront só existe após o 1º)

```bash
cd infra
cp homol.tfvars.example homol.tfvars        # preencha account_suffix e cognito_domain_prefix
tofu init
tofu workspace new homol                     # state local separado do dev
tofu apply -var-file=homol.tfvars -var="environment=homol"   # 1º apply (callback localhost)
```

Anote os outputs:

```bash
tofu output homol_web_url      # https://dxxxx.cloudfront.net
tofu output ecr_registry       # <account>.dkr.ecr.sa-east-1.amazonaws.com
tofu output deploy_bucket      # easycob-homol-<sufixo>
tofu output cognito_domain     # https://easycob-homol-...auth.sa-east-1.amazoncognito.com
tofu output cognito_client_id
tofu output cognito_authority
tofu output queue_url          # easycob-homol-events
```

2º apply — configure os callbacks do Cognito com o domínio CloudFront:

```bash
# em homol.tfvars:
cognito_callback_urls = ["https://dxxxx.cloudfront.net/api/auth/callback"]
cognito_logout_urls   = ["https://dxxxx.cloudfront.net/login"]
tofu apply -var-file=homol.tfvars -var="environment=homol"
```

## 3. Seed do primeiro tenant e usuário

```bash
TENANT_ID=$(cat /proc/sys/kernel/random/uuid)
docker compose exec postgres psql -U postgres -d easycob -c \
  "INSERT INTO tenants (id,name,time_zone,currency,created_at) VALUES ('$TENANT_ID','Empresa homolog','America/Sao_Paulo','BRL',now());"
```

O banco está dentro da EC2; faça o seed pela API da EC2 ou:

```bash
aws ssm start-session --target <INSTANCE_ID>
docker exec easycob-postgres-1 psql -U postgres -d easycob -c \
  "INSERT INTO tenants ..."
```

Crie o usuário Cognito com o mesmo `TENANT_ID` em `custom:tenant_id` e associe o grupo (mesmos comandos de `docs/cognito.md`, com o `USER_POOL_ID` de homol).

## 4. Secrets no GitHub (repo `marcusnog/easycob`)

| Secret | Valor |
|---|---|
| `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` | credencial com ECR push, S3 put, SSM send-command, EC2 describe |
| `ECR_REGISTRY` | output `ecr_registry` |
| `HOMOL_DEPLOY_BUCKET` | output `deploy_bucket` |
| `HOMOL_AUTH_AUTHORITY` | output `cognito_authority` |
| `HOMOL_AUTH_AUDIENCE` | output `cognito_client_id` |
| `HOMOL_COGNITO_DOMAIN` | output `cognito_domain` |
| `HOMOL_COGNITO_CLIENT_ID` | output `cognito_client_id` |
| `HOMOL_QUEUE_URL` | output `queue_url` |
| `HOMOL_WHATSAPP_VERIFY_TOKEN` / `_APP_SECRET` / `_ACCESS_TOKEN` | credenciais da Meta WhatsApp Cloud API |

Proteja `main` (Settings → Branches): exigir PR + status checks `api`/`web` do CI.

## 5. Deploy

Merge na `main` (ou `workflow_dispatch`): CI roda, CD faz build das 3 imagens no ECR, envia compose/.env/init.sql para o bucket e executa `docker compose up -d --pull always` na EC2 via SSM.

Acesso: `https://dxxxx.cloudfront.net` (o CloudFront faz redirect para HTTPS).

## 6. Operação

- **Desligamento automático**: stop 23h BRT / start 7h BRT (EventBridge + Lambda `easycob-homol-shutdown`). Para forçar: `aws ec2 stop-instances --instance-ids <id>`.
- **Logs**: `aws ssm start-session --target <id>` → `docker compose -f /opt/easycob/compose.yaml logs -f --tail=200`.
- **Rollback**: `workflow_dispatch` no CD não reverte; rode o deploy manual do SHA anterior:
  ```bash
  ./deploy/homol-deploy.sh   # com IMAGE_TAG=<sha-anterior>
  ```
- **Orçamento**: alerta em 80% do limite mensal (`aws_budgets_budget`).

## 7. Produção

A job `deploy-production` existe no CD com gate de aprovação (`environment: production`) e reusa `deploy/homol-deploy.sh` com secrets `PROD_*`. Fica dormante até existir host/infra de produção (ECS Fargate + RDS por gatilhos do PLANEJAMENTO §125).
