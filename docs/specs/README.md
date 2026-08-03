# Especificação do EasyCob

Versão: 1.0  
Escopo: MVP  
Fonte arquitetural: [`PLANEJAMENTO.md`](../../PLANEJAMENTO.md)

Este documento é a fonte de verdade funcional e técnica do MVP. Cada requisito possui um identificador estável para rastreamento em issues, testes e releases.

## 1. Objetivo e limites

O EasyCob permite que empresas mantenham clientes, emitam cobranças, registrem pagamentos e automatizem lembretes pelo WhatsApp, sempre com isolamento por empresa.

Faz parte do MVP: multi-tenancy, RBAC, clientes e contatos, consentimento, cobranças e parcelas, pagamentos manuais, mensagens e régua de cobrança, indicadores financeiros, auditoria, importação/exportação e operação em AWS.

Não faz parte do MVP: Pix, boleto, conciliação bancária, push/offline, microserviços físicos, Kubernetes, campanhas de marketing e aplicativo nativo.

## 2. Papéis

| Papel | Capacidades |
|---|---|
| Owner | todas as operações, inclusive configurações, usuários e LGPD |
| Admin | administração operacional, sem assumir propriedade do tenant |
| Finance | clientes, cobranças, pagamentos, importação e indicadores |
| Collector | clientes, contatos, cobranças e acompanhamento de mensagens |
| Viewer | consultas e indicadores, sem alterações |

### Regras de identidade e tenancy

- **AUTH-001** A autenticação deve ser delegada ao Amazon Cognito por Authorization Code com PKCE.
- **AUTH-002** O frontend deve manter tokens apenas em cookie `httpOnly`, `secure` em produção e `sameSite=lax`.
- **AUTH-003** Toda rota privada deve rejeitar usuário não autenticado.
- **AUTH-004** Toda mutação deve exigir um dos papéis explicitamente autorizados.
- **TEN-001** O `tenant_id` deve vir exclusivamente de claim assinada e nunca do corpo, query string ou header fornecido pelo cliente em produção.
- **TEN-002** Toda entidade de negócio deve possuir `tenant_id` obrigatório.
- **TEN-003** Toda consulta e alteração deve ser filtrada pelo tenant atual, inclusive buscas por identificador.
- **TEN-004** O PostgreSQL deve aplicar RLS como segunda barreira.
- **TEN-005** Índices e unicidade de dados de negócio devem começar ou ser limitados por `tenant_id`.

## 3. Clientes e LGPD

- **CUS-001** Finance e Collector podem cadastrar cliente com nome obrigatório e documento opcional.
- **CUS-002** Documento informado deve ser único dentro do tenant; duplicidade deve retornar conflito.
- **CUS-003** A listagem deve omitir clientes arquivados, ordenar por nome, aceitar busca por nome/documento e paginar em blocos de 50.
- **CUS-004** Owner e Admin podem arquivar clientes sem apagamento físico.
- **CUS-005** Contato deve possuir telefone válido de 10 a 15 dígitos ou e-mail.
- **CUS-006** Opt-in de WhatsApp exige telefone e deve registrar o instante do consentimento.
- **CUS-007** Opt-out deve impedir novos envios e registrar seu instante.
- **CUS-008** Importação CSV deve ser streaming, aceitar até 10 MiB, validar cada linha e ignorar documento já existente.
- **CUS-009** Owner e Admin podem exportar os dados completos de um titular.
- **CUS-010** Anonimização deve remover PII e arquivar o cliente, mas ser recusada enquanto houver cobrança aberta.

## 4. Cobranças e pagamentos

- **BIL-001** Cobrança exige cliente ativo do tenant, descrição, valor positivo, primeiro vencimento e 1 a 120 parcelas.
- **BIL-002** Valores monetários devem usar decimal com duas casas e arredondamento `AwayFromZero`.
- **BIL-003** A soma das parcelas deve ser exatamente igual ao total; eventual centavo residual fica na primeira parcela.
- **BIL-004** Vencimentos mensais devem preservar o dia quando possível e usar o último dia em meses menores.
- **BIL-005** Estados válidos são Aberta, Em atraso, Parcialmente paga, Paga, Cancelada e Negociada.
- **BIL-006** Pagamento exige valor positivo, data não futura e não pode exceder o saldo.
- **BIL-007** `external_id` de pagamento deve ser idempotente dentro do tenant.
- **BIL-008** Pagamento parcial altera o estado para Parcialmente paga; quitação exata altera para Paga.
- **BIL-009** Cobrança com pagamento não pode ser cancelada.
- **BIL-010** Criação, pagamento e cancelamento devem gerar evento na outbox na mesma transação dos dados.

## 5. Mensageria e WhatsApp

- **MSG-001** Owner e Admin podem versionar templates aprovados pela Meta.
- **MSG-002** Owner e Admin podem criar e ativar regras entre -365 e 365 dias do vencimento.
- **MSG-003** Criar cobrança para contato com opt-in deve agendar uma mensagem para cada regra ativa, sem duplicidade observável.
- **MSG-004** O dispatcher deve enviar somente mensagens vencidas no estado Pendente.
- **MSG-005** Chamadas à Meta devem ter timeout, retry com jitter para rede/408/429/5xx e circuit breaker.
- **MSG-006** Uma entrega ambígua deve permanecer em Enviando até reconciliação explícita.
- **MSG-007** Somente mensagens Falhou podem ser reenviadas manualmente.
- **MSG-008** Webhook deve validar `X-Hub-Signature-256`, tamanho e estrutura antes de persistir efeitos.
- **MSG-009** Evento externo repetido deve ser aceito sem repetir a transição, usando inbox com chave única.
- **MSG-010** Mensagem que exceder tentativas deve seguir para DLQ e ação humana.
- **MSG-011** Logs não podem conter token, corpo integral da conversa ou PII desnecessária.

## 6. Financeiro

- **FIN-001** A receber é o total não cancelado/não pago menos pagamentos associados.
- **FIN-002** Em atraso é o total de cobranças vencidas menos pagamentos associados.
- **FIN-003** Recebido é a soma de pagamentos confirmados.
- **FIN-004** Consulta diária aceita no máximo 366 dias e rejeita período invertido.
- **FIN-005** Valores do dashboard devem respeitar o tenant e a consistência da projeção documentada.

## 7. Auditoria

- **AUD-001** Criação, alteração, arquivamento, anonimização, pagamento, cancelamento, consentimento e ações manuais de mensagem devem gerar evento de auditoria.
- **AUD-002** Evento deve registrar tenant, ator, ação, tipo/alvo, instante UTC e metadados mínimos, sem PII desnecessária.
- **AUD-003** A trilha deve ser imutável para usuários da aplicação e paginada do evento mais recente para o mais antigo.

## 8. Frontend

- **WEB-001** O aplicativo deve usar Next.js App Router e componentes de servidor por padrão.
- **WEB-002** Usuário não autenticado deve ser direcionado ao login; retorno Cognito inválido deve falhar sem gravar token.
- **WEB-003** Navegação autenticada deve oferecer Visão geral, Clientes, Cobranças, Mensagens e saída.
- **WEB-004** Dashboard deve apresentar A receber, Em atraso e Recebido em BRL.
- **WEB-005** Cliente deve poder ser cadastrado e listado.
- **WEB-006** Cobrança à vista ou parcelada deve poder ser criada e listada com status.
- **WEB-007** Mensagens devem ser listadas com agendamento, status, tentativas e falha.
- **WEB-008** A interface deve funcionar desde 320 px, por teclado, com foco visível, rótulos e mensagens de erro anunciadas.
- **WEB-009** Em telas estreitas, conteúdo não pode provocar rolagem horizontal da página; tabelas podem ter região própria rolável ou modo cartão.
- **WEB-010** Dados autenticados não devem ser armazenados em cache público nem persistidos offline.

## 9. API e contratos

- **API-001** A API deve usar JSON, HTTPS fora do desenvolvimento e códigos HTTP semânticos.
- **API-002** Erros de validação devem usar Problem Details com erros por campo.
- **API-003** Recursos criados devem retornar `201` e localização; remoções/alterações sem corpo devem retornar `204`.
- **API-004** OpenAPI deve estar disponível para geração do cliente e validação de contrato.
- **API-005** Endpoints de lista devem ter paginação limitada; filtros não podem remover a restrição de tenant.
- **API-006** `/health/live` não consulta dependências; `/health/ready` valida PostgreSQL.

## 10. Dados e eventos

- **DAT-001** Migrations devem ser versionadas, compatíveis com rolling deploy e executadas como job controlado.
- **DAT-002** Chaves estrangeiras compostas devem impedir associação entre tenants.
- **DAT-003** Instantes devem ser armazenados em UTC; datas de vencimento usam data civil.
- **DAT-004** Outbox deve ser publicada pelo worker e marcada somente após aceitação pela fila.
- **DAT-005** Consumidor deve registrar inbox antes de concluir efeito, garantindo processamento idempotente.
- **DAT-006** Contratos de evento devem possuir nome e versão, carregar identificadores pequenos e permanecer retrocompatíveis.

## 11. Operação, infraestrutura e segurança

- **OPS-001** API, worker e web devem ser imagens independentes e executáveis localmente por Docker Compose.
- **OPS-002** Produção inicial deve usar SQS Standard com DLQ, PostgreSQL gerenciado e segredos fora das imagens.
- **OPS-003** Logs devem conter `trace_id`, tenant pseudonimizado, módulo e resultado.
- **OPS-004** Devem existir métricas para cobranças, valor vencido, mensagens por status, idade da fila, DLQ e erros da Meta.
- **OPS-005** Alarmes mínimos: API indisponível, fila envelhecendo, DLQ maior que zero, webhook falhando, backup falhando e orçamento.
- **OPS-006** Backup deve ter retenção definida e restauração comprovada em ambiente isolado antes de produção.
- **OPS-007** Runbooks devem cobrir indisponibilidade do WhatsApp, fila acumulada, banco indisponível, segredo comprometido e restauração.
- **SEC-001** Credenciais, payloads reais, tokens e `.env` nunca entram no repositório.
- **SEC-002** Dependências e imagens devem ser verificadas por vulnerabilidades no CI.
- **SEC-003** Entrada externa deve possuir limite de tamanho, validação e rate limit proporcional ao risco.
- **SEC-004** TLS e criptografia em repouso são obrigatórios em produção.

## 12. Cenários de aceite

```gherkin
Funcionalidade: isolamento entre empresas
  Cenário: usuário tenta acessar recurso de outro tenant
    Dado um usuário autenticado no tenant A
    E um recurso pertencente ao tenant B
    Quando o usuário consulta ou altera o recurso pelo identificador
    Então nenhum dado do tenant B é retornado ou alterado

Funcionalidade: emissão de cobrança
  Cenário: cobrança parcelada com régua ativa
    Dado um cliente com consentimento de WhatsApp
    E três regras de cobrança ativas
    Quando Finance cria uma cobrança em três parcelas
    Então a soma das parcelas é igual ao valor da cobrança
    E três mensagens são agendadas
    E um evento de criação é gravado na outbox

Funcionalidade: webhook idempotente
  Cenário: Meta repete uma confirmação de entrega
    Dado um webhook com assinatura válida
    Quando o mesmo identificador externo é recebido duas vezes
    Então ambas as requisições são aceitas
    E apenas uma entrada de inbox e uma transição são persistidas

Funcionalidade: quitação
  Cenário: pagamentos completam o valor da cobrança
    Dado uma cobrança aberta de R$ 100,00
    Quando são registrados pagamentos de R$ 30,00 e R$ 70,00
    Então a cobrança fica Paga
    E o dashboard soma R$ 100,00 em Recebido

Funcionalidade: experiência móvel acessível
  Cenário: operação por teclado em viewport estreita
    Dado a aplicação com largura de 320 px
    Quando o usuário navega e cadastra cliente e cobrança sem mouse
    Então todos os controles possuem foco visível e nome acessível
    E nenhuma ação fica inacessível por rolagem horizontal da página
```

## 13. Matriz de verificação

| Grupo | Verificação mínima |
|---|---|
| AUTH/TEN/SEC | integração + arquitetura + revisão de configuração |
| CUS/BIL/FIN | unidade para dinheiro/estado; integração para persistência e autorização |
| MSG/DAT | integração com PostgreSQL/SQS/fake Meta e contrato sanitizado |
| WEB | lint, tipos, build e E2E das três jornadas principais |
| OPS | smoke test, teste de carga, restore e exercício dos runbooks |

Definition of Done: requisito atendido, teste proporcional ao risco aprovado, telemetria e autorização verificadas, migration compatível quando aplicável e documentação operacional atualizada.
