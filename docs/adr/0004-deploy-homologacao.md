# ADR 0004: Deploy de homologação em EC2 com Docker Compose

Status: aceito

## Contexto

O monólito (api, web, worker) precisa de um ambiente de homologação antes de produção (PLANEJAMENTO: "Deploy primeiro em homologação; produção com aprovação"). Não há domínio próprio disponível; o Cognito exige callback HTTPS fora de localhost. Não há ainda compute na infra (Cognito/SQS/S3/Lambda apenas).

## Decisão

- Homologação roda como um único host **EC2 (Amazon Linux 2023) com Docker Compose**: `postgres`, `migrate`, `api`, `web`, `worker`. Banco em container é aceitável na homologação (PLANEJAMENTO §118); Fargate/RDS ficam adiados para os gatilhos do §125.
- **CloudFront** fornece HTTPS com o domínio padrão `*.cloudfront.net` (sem custo e sem domínio próprio), resolvendo o callback HTTPS do Cognito. A API fica interna (`http://api:8080`), pois todo acesso do web é server-side (server components/actions).
- **ECR** armazena imagens por `sha` (imutável, rollback = redeploy do SHA anterior) + `latest`.
- **Deploy via GitHub Actions** com SSM Run Command (sem chave SSH): CI em PR/push, CD em merge na `main` → homologação; produção com gate manual (`environment: production`) e target dormante até existir infra de produção.
- **Custo desde o dia 1** (PLANEJAMENTO §129): `aws_budgets_budget` com alerta por e-mail e desligamento automático da EC2 fora do horário (EventBridge + Lambda, stop 23h / start 7h BRT).

## Alternativas consideradas

- **ECS Fargate + RDS agora**: rejeitado — custo e complexidade que o plano reserva para gatilhos de produção.
- **Amplify para o web**: rejeitado — divide o deploy e o SSR/server actions no Amplify ficam atrelados à plataforma.
- **Cert self-signed na EC2**: rejeitado — aviso de segurança no navegador inviabiliza o login E2E.

## Consequências

- Dois passos de `tofu apply`: o domínio CloudFront só é conhecido após o primeiro apply, então o segundo seta os `cognito_callback_urls` (documentado no runbook).
- Rollback de homologação/produção é redeploy da imagem do SHA anterior via `workflow_dispatch`.
