# Cognito no EasyCob

## Provisionamento

Pré-requisitos no WSL: OpenTofu e credenciais AWS válidas. Nunca grave credenciais em `.env` ou `tfvars`.

```bash
cd infra
tofu init
tofu apply \
  -var="account_suffix=SUFIXO_GLOBAL_UNICO" \
  -var="cognito_domain_prefix=easycob-dev-SUFIXO_GLOBAL_UNICO"
```

Copie os outputs `cognito_domain`, `cognito_client_id`, `cognito_authority`, `queue_url` e `bucket_name` para o `.env` da raiz:

```env
COGNITO_DOMAIN=https://DOMINIO.auth.sa-east-1.amazoncognito.com
COGNITO_CLIENT_ID=CLIENT_ID
Authentication__Authority=https://cognito-idp.sa-east-1.amazonaws.com/USER_POOL_ID
Authentication__Audience=CLIENT_ID
```

## Primeiro tenant e usuário

Crie um UUID e persista o tenant:

```bash
TENANT_ID=$(cat /proc/sys/kernel/random/uuid)
docker compose exec postgres psql -U postgres -d easycob -c \
  "INSERT INTO tenants (id,name,time_zone,currency,created_at) VALUES ('$TENANT_ID','Empresa inicial','America/Sao_Paulo','BRL',now());"
```

Crie o usuário Cognito, grave o mesmo UUID em `custom:tenant_id` e associe o grupo. Os valores `USER_POOL_ID` vêm dos outputs:

```bash
aws cognito-idp admin-create-user --user-pool-id USER_POOL_ID --username dono@empresa.com \
  --user-attributes Name=email,Value=dono@empresa.com Name=email_verified,Value=true Name=custom:tenant_id,Value=$TENANT_ID
aws cognito-idp admin-add-user-to-group --user-pool-id USER_POOL_ID --username dono@empresa.com --group-name Owner
```

Ao primeiro login, a API cria a associação local pelo `sub` assinado, tenant e grupo recebidos no access token.

## Execução

```bash
docker compose up --build
```

Abra `http://localhost:3001`. O app usa Authorization Code + PKCE, cookies `httpOnly`, refresh token e logout do Cognito. Para produção, inclua URLs HTTPS nas variáveis `cognito_callback_urls` e `cognito_logout_urls`.
