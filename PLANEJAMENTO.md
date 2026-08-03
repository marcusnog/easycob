# EasyCob — planejamento de arquitetura e desenvolvimento

Status: proposta para aprovação  
Stack-base: .NET 10, PostgreSQL, Next.js/React, AWS, WhatsApp Cloud API

## 1. Decisão arquitetural

Construir um **monólito modular com arquitetura orientada a eventos**, preparado para extração de microserviços, e não vários microserviços físicos no MVP.

Isso preserva os limites de domínio pedidos, mas começa com apenas três unidades implantáveis:

1. `web`: Next.js responsivo/PWA;
2. `api`: ASP.NET Core 10 com os módulos de negócio;
3. `worker`: .NET 10 para filas, agendamentos, webhooks e envio de mensagens.

O primeiro serviço a ser separado quando houver necessidade real será `Messaging`, pois a integração com WhatsApp tem escala, falhas e ritmo de entrega próprios. Cobrança e clientes permanecem juntos até métricas mostrarem necessidade de separação.

## 2. Contextos de negócio

| Módulo | Responsabilidade | Dono dos dados |
|---|---|---|
| Identity & Tenancy | empresa, usuários, papéis, planos e configuração | tenants, users, roles |
| Customers | carteira de clientes, contatos, consentimento e preferências | customers, contacts |
| Billing | títulos, parcelas, vencimentos, baixa, negociação e status | charges, installments, payments |
| Messaging | templates, campanhas, conversas, opt-out, envio e status WhatsApp | messages, conversations, deliveries |
| Finance | visão de caixa, inadimplência e indicadores | projeções derivadas de Billing |
| Audit | trilha imutável das ações sensíveis | audit_events |

Cada módulo terá suas próprias pastas, casos de uso, tabelas/esquema e contratos internos. Um módulo não acessa diretamente as tabelas de outro. No MVP, todos compartilham o mesmo processo e a mesma instância PostgreSQL; isso reduz custo sem misturar responsabilidades.

## 3. Fluxo principal

1. Usuário autenticado acessa o tenant por subdomínio ou tenant selecionado.
2. A API resolve `tenant_id` a partir de uma claim assinada, nunca de um valor livre enviado pelo cliente.
3. A cobrança é persistida e um evento é salvo na mesma transação em `outbox_messages`.
4. O worker publica o evento no SQS e agenda a régua de cobrança.
5. O worker de mensagens chama a WhatsApp Cloud API usando `HttpClientFactory` e resiliência.
6. Webhooks da Meta entram por endpoint público, têm assinatura validada, são deduplicados e atualizam entrega/conversa.
7. Eventos atualizam as projeções financeiras; a UI consulta projeções prontas, não recalcula a carteira inteira.

Garantias: entrega **pelo menos uma vez**, consumidores idempotentes, chave única por evento externo e DLQ para mensagens que excederem tentativas.

## 4. Multi-tenancy e segurança

- Banco compartilhado e schema compartilhado, com `tenant_id` obrigatório em toda tabela de negócio.
- PostgreSQL Row-Level Security como segunda barreira, além dos filtros da aplicação.
- Índices compostos iniciados por `tenant_id`; unicidade sempre limitada ao tenant quando aplicável.
- Autenticação com Amazon Cognito no MVP; evita manter senhas, MFA, recuperação e rotação internamente. Avaliar Keycloak apenas se o custo por usuários ativos superar comprovadamente o custo operacional de hospedá-lo.
- RBAC inicial: `Owner`, `Admin`, `Finance`, `Collector`, `Viewer`.
- Segredos no AWS Systems Manager Parameter Store/Secrets Manager conforme necessidade de rotação.
- Criptografia TLS em trânsito e volumes/buckets criptografados em repouso.
- LGPD: consentimento/finalidade, opt-out, retenção configurável, exportação/exclusão, minimização de PII e auditoria de acesso.
- Webhooks: validação de assinatura, limite de tamanho, rate limit, timestamp e deduplicação.
- Nunca registrar tokens, conteúdo completo de conversas ou PII desnecessária.

## 5. Resiliência com Polly

Usar `Microsoft.Extensions.Http.Resilience`, baseado em Polly. Não usar o pacote legado `Microsoft.Extensions.Http.Polly`.

Política por integração, não uma política global:

- WhatsApp: timeout curto, circuit breaker e retry exponencial com jitter somente para falhas transitórias (`408`, `429`, `5xx` e rede).
- Não repetir automaticamente requisições não idempotentes sem uma chave de idempotência/controlador de duplicidade.
- Respeitar `Retry-After` da Meta.
- Banco: retry apenas para falhas transitórias reconhecidas pelo provider; transações curtas.
- SQS: visibility timeout maior que o tempo máximo do job, long polling, batch e DLQ.
- Bulkhead/limite de concorrência por tenant para impedir que uma empresa monopolize o envio.
- Health checks separados: `/health/live` sem dependências e `/health/ready` para prontidão.

## 6. Mensageria: SQS versus RabbitMQ

| Critério | Amazon SQS | RabbitMQ / Amazon MQ |
|---|---|---|
| Modelo de custo | por requisição, sem mínimo | broker e armazenamento ligados continuamente |
| Operação | totalmente gerenciado e autoescalável | exige sizing, upgrades e observação do broker |
| Recursos | filas Standard/FIFO, delay, DLQ | roteamento avançado, exchanges, plugins |
| Adequação ao MVP | alta | baixa, sem requisito de roteamento avançado |

**Decisão: SQS Standard + DLQ.** Usar FIFO somente onde ordem por cobrança for requisito comprovado. Adotar SNS apenas quando um evento precisar alimentar múltiplos consumidores independentes. RabbitMQ só entra se surgirem requisitos reais de exchanges, roteamento complexo, baixa latência de broker ou portabilidade multicloud que compensem seu custo fixo.

Mensagens carregam identificadores e metadados pequenos; anexos ficam no S3. Contratos são versionados e compatíveis para trás.

## 7. Frontend Next.js e microfrontends

Começar com **um único Next.js App Router**, responsivo e instalável como PWA, organizado por domínios:

```text
apps/web/app/(auth)
apps/web/app/(dashboard)/clientes
apps/web/app/(dashboard)/cobrancas
apps/web/app/(dashboard)/financeiro
apps/web/features/{customers,billing,finance,messaging}
```

Isso entrega fronteiras de microfrontend sem duplicar deploy, autenticação, design system e dependências. Componentes de servidor por padrão; componentes de cliente apenas para interação. API/BFF do Next.js somente para necessidades da experiência web, mantendo regras de negócio na API .NET.

Quando duas equipes precisarem publicar independentemente ou o tempo de build virar gargalo mensurável, extrair uma área para **Next.js Multi-Zones**, roteada por caminho. Evitar Module Federation no início. Áreas muito navegadas em conjunto devem permanecer na mesma zona, pois a troca entre zonas causa navegação completa.

Requisitos mobile-web: abordagem mobile-first, WCAG 2.2 AA, tabelas com modo cartão em telas pequenas, formulários acessíveis, metas de Core Web Vitals e cache apenas de assets/dados não sensíveis. Notificações push/offline ficam fora do MVP.

## 8. Dados e integrações

- PostgreSQL + EF Core; migrations versionadas e executadas como job controlado no deploy.
- `decimal` com precisão explícita para dinheiro; moeda e timezone do tenant configuráveis.
- Datas de negócio com timezone explícito; armazenar instantes em UTC.
- S3 para arquivos/importações/exportações, com URLs pré-assinadas e lifecycle.
- WhatsApp Cloud API por adaptador fino; persistir IDs externos, template/versão, consentimento e status.
- Importação CSV em streaming, validada e processada pelo worker.
- Outbox transacional na escrita e inbox/deduplicação no consumo.
- Backup automático e teste periódico de restauração; RPO/RTO serão aprovados antes da produção.

## 9. AWS com foco em baixo custo

### MVP/piloto

- Região `sa-east-1` se residência/latência no Brasil for requisito; comparar `us-east-1` se não for.
- Uma instância Linux ARM/Graviton (Lightsail ou EC2 `t4g`) executando os três containers via Docker Compose.
- PostgreSQL gerenciado pequeno para produção; em desenvolvimento/homologação, PostgreSQL em container é aceitável.
- SQS, S3, CloudFront, Route 53, Cognito e Parameter Store.
- Caddy ou ALB apenas conforme o modelo escolhido; evitar NAT Gateway no MVP pelo custo fixo.
- CloudWatch com retenção curta e OpenTelemetry; métricas de negócio essenciais. Grafana/Tempo/Loki só quando houver necessidade operacional.

### Escala/alta disponibilidade

Migrar containers para ECS Fargate ou ECS sobre EC2, banco para RDS Multi-AZ, incluir ALB, autoscaling e sub-redes privadas. Fazer isso por gatilhos: SLA contratado, indisponibilidade não aceitável, saturação sustentada ou equipe sem capacidade de operar a instância única.

Não iniciar com EKS/Kubernetes, service mesh, Kafka, Redis, API Gateway, EventBridge ou OpenSearch. Cada um será adicionado somente diante de um requisito medido.

Controles financeiros: AWS Budgets e alarmes desde o primeiro dia, tags por ambiente/produto, retenção de logs, lifecycle do S3, desligamento automático de homologação e revisão mensal de custo por tenant/mensagem.

## 10. Estrutura do repositório

```text
apps/
  api/                  # ASP.NET Core 10, módulos e endpoints
  worker/               # consumidores, scheduler e outbox publisher
  web/                  # Next.js
tests/
  architecture/         # limites entre módulos e tenant isolation
  integration/          # PostgreSQL/SQS/WhatsApp fake
infra/                  # OpenTofu/Terraform mínimo por ambiente
docs/adr/               # decisões realmente tomadas
```

Monorepo, contratos OpenAPI gerados pela API e cliente TypeScript gerado no build. `main` protegida, PR pequena, CI com lint/build/test/SAST e imagem imutável. Deploy primeiro em homologação; produção com aprovação e rollback para a imagem anterior.

## 11. Observabilidade e operação

- Logs estruturados com `trace_id`, `tenant_id` pseudonimizado, módulo e resultado.
- OpenTelemetry para traces, métricas e logs; exportação inicialmente ao CloudWatch.
- Métricas: cobranças criadas/baixadas, valor vencido, mensagens por status, idade da fila, DLQ, taxa de erro da Meta e custo estimado por tenant.
- Alarmes: API indisponível, fila envelhecendo, DLQ > 0, erro de webhook, falha de backup e orçamento.
- Runbooks mínimos: WhatsApp fora, fila acumulada, banco indisponível, segredo comprometido e restauração.

## 12. Qualidade e testes

- Unitários somente para regras de cobrança, dinheiro, calendário e autorização.
- Integração para isolamento entre tenants, outbox/inbox, webhooks, idempotência e migrations.
- Contract tests do adaptador WhatsApp com fixtures sanitizadas.
- E2E para três jornadas: cadastrar cliente, emitir cobrança, enviar/acompanhar cobrança.
- Teste de carga antes do piloto e teste de recuperação de backup antes de produção.
- Definition of Done: segurança, telemetria, migration reversível/compatível, teste proporcional ao risco e documentação da operação.

## 13. Fases e entregas

### Fase 0 — Descoberta (1–2 semanas)

- Jornada de cobrança, personas, papéis, política de consentimento e templates.
- Volume esperado: tenants, clientes, cobranças/dia e mensagens/dia.
- Provedor oficial/BSP do WhatsApp e regras comerciais.
- SLO, RPO/RTO, residência de dados e critérios do piloto.
- ADRs: tenancy, SQS, deploy inicial, autenticação e WhatsApp.

### Fase 1 — Fundação (2 semanas)

- Monorepo, CI/CD, ambientes, autenticação, tenant isolation, auditoria e observabilidade.
- API/worker/web implantados, PostgreSQL, SQS/DLQ, S3 e budgets.

### Fase 2 — MVP funcional (4–6 semanas)

- Clientes/importação, títulos/parcelas, dashboard financeiro inicial.
- Templates, consentimento, régua simples, envio WhatsApp, webhooks e histórico.
- RBAC, exportação e trilha de auditoria.

### Fase 3 — Piloto e endurecimento (2–3 semanas)

- E2E/carga/segurança, restore, runbooks, acessibilidade e ajustes de UX.
- Piloto com poucos tenants, limites por plano e acompanhamento de custo.

### Fase 4 — Evolução orientada por métricas

- Pagamentos/boletos/Pix e conciliação, se aprovados.
- Separar Messaging, Multi-Zones, Redis ou infraestrutura HA apenas quando os gatilhos ocorrerem.

Estimativa inicial: 9–13 semanas para piloto com uma equipe pequena e experiente (2 backend, 1 frontend, 1 QA/produto compartilhado). Refinar após a Fase 0.

## 14. Critérios de aceite do MVP

- Nenhum usuário consegue consultar ou alterar dados de outro tenant, inclusive por ID previsível.
- Cobrança criada gera régua e mensagem sem duplicidade observável.
- Webhooks repetidos não duplicam estados ou lançamentos.
- Falha temporária da Meta é retomada; falha permanente vai para DLQ/ação humana.
- Dashboard apresenta carteira, vencido, recebido e taxa de entrega com consistência definida.
- Backup restaurado em ambiente isolado e alarmes/runbooks exercitados.
- Uso completo em viewport mobile e navegação por teclado.

## 15. Pontos que exigem aprovação

1. Aceitar monólito modular implantável como API + worker no MVP, mantendo fronteiras para microserviços futuros.
2. Aceitar um Next.js modular no MVP e Multi-Zones somente por gatilho de equipe/build.
3. Escolher SQS Standard como fila inicial.
4. Confirmar Cognito como identidade inicial.
5. Confirmar região e nível de disponibilidade/custo do piloto.
6. Definir se pagamento/Pix/boletos faz parte do MVP ou de uma fase posterior.

## Referências oficiais consultadas

- AWS: Amazon SQS Pricing — https://aws.amazon.com/sqs/pricing/
- AWS: Amazon MQ Pricing — https://aws.amazon.com/amazon-mq/pricing/
- AWS: Amazon Lightsail Pricing — https://aws.amazon.com/lightsail/pricing/
- AWS: EC2 On-Demand Pricing — https://aws.amazon.com/ec2/pricing/on-demand/
- Microsoft: Resilient app development — https://learn.microsoft.com/dotnet/core/resilience/
- Microsoft: Resilient HTTP apps — https://learn.microsoft.com/dotnet/core/resilience/http-resilience
- Next.js: Multi-Zones — https://nextjs.org/docs/app/guides/multi-zones
- Next.js: Multi-tenant — https://nextjs.org/docs/app/guides/multi-tenant

## Limitação da análise

O MCP do Obsidian não estava disponível nesta sessão e não havia conteúdo local do HealthManager no workspace; portanto, nenhuma prática específica dele foi presumida. Assim que o vault ou a nota for disponibilizado, este planejamento deve passar por uma revisão comparativa curta, preservando somente padrões que se apliquem ao EasyCob.
