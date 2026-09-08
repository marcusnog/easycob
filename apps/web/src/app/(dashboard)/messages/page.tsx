import { api } from "@/lib/api";
import ConfigForms from "./config-forms";
import { retryMessage, setRuleActive } from "./actions";
import ReconciliationForm from "./reconciliation-form";

type Message = { id: string; chargeId?: string; status: number; scheduledAt: string; sentAt?: string; attempts: number; failureCode?: string };
type Template = { id: string; name: string; metaTemplateId: string; language: string; version: number };
type Rule = { id: string; name: string; messageTemplateId: string; daysOffset: number; active: boolean };
const statuses = ["Pendente", "Enviada", "Entregue", "Lida", "Falhou", "Enviando"];
const pillClass = ["warn", "info", "ok", "ok", "bad", "info"];

export default async function MessagesPage() {
  let messages: Message[] = [];
  let templates: Template[] = [], rules: Rule[] = [];
  let role = 4;
  let failed = false;
  try { [messages, templates, rules, { role }] = await Promise.all([api<Message[]>("/messages"), api<Template[]>("/message-templates"), api<Rule[]>("/collection-rules"), api<{ role: number }>("/tenant/me")]); } catch { failed = true; }
  const templateNames = new Map(templates.map(template => [template.id, `${template.name} · v${template.version}`]));

  return (
    <>
      <div className="page-header">
        <div>
          <p className="eyebrow">Régua de cobrança</p>
          <h1>Mensagens</h1>
          <p className="muted">Acompanhe as cobranças enviadas pelo WhatsApp e o estado de cada envio.</p>
        </div>
      </div>
      {role <= 1 && <>
        <ConfigForms templates={templates} />
        <section className="card table-wrap" style={{ margin: "1.5rem 0" }} aria-labelledby="rules-list">
          <div className="table-card-header"><h2 id="rules-list">Regras da régua</h2><span className="pill">{rules.length} regra(s)</span></div>
          {rules.length === 0 ? <p className="muted">Nenhuma regra configurada.</p> : <table>
            <thead><tr><th>Nome</th><th>Template</th><th>Quando</th><th>Status</th><th>Ação</th></tr></thead>
            <tbody>{rules.map(rule => <tr key={rule.id}>
              <td data-label="Nome">{rule.name}</td>
              <td data-label="Template">{templateNames.get(rule.messageTemplateId) ?? "—"}</td>
              <td data-label="Quando">{rule.daysOffset === 0 ? "No vencimento" : rule.daysOffset < 0 ? `${Math.abs(rule.daysOffset)} dia(s) antes` : `${rule.daysOffset} dia(s) depois`}</td>
              <td data-label="Status"><span className={`pill ${rule.active ? "ok" : "warn"}`}>{rule.active ? "Ativa" : "Inativa"}</span></td>
              <td data-label="Ação"><form action={setRuleActive.bind(null, rule.id, !rule.active)}><button className="btn btn-secondary" type="submit">{rule.active ? "Desativar" : "Ativar"}</button></form></td>
            </tr>)}</tbody>
          </table>}
        </section>
      </>}
      <section className="card table-wrap">
        <div className="table-card-header">
          <h2>Últimos envios</h2>
          {!failed && <span className="pill">{messages.length === 1 ? "1 mensagem" : `${messages.length} mensagens`}</span>}
        </div>
        {failed ? (
          <p className="error" role="alert">Não foi possível carregar as mensagens.</p>
        ) : messages.length === 0 ? (
          <p className="empty"><strong>Nenhuma mensagem agendada</strong>As cobranças pendentes geram mensagens de WhatsApp automaticamente.</p>
        ) : (
          <table>
            <thead><tr><th>Agendamento</th><th>Status</th><th>Tentativas</th><th>Falha</th><th>Ação</th></tr></thead>
            <tbody>{messages.map(message =>
              <tr key={message.id}>
                <td data-label="Agendamento">{new Date(message.scheduledAt).toLocaleString("pt-BR")}</td>
                <td data-label="Status"><span className={`pill ${pillClass[message.status] ?? ""}`}>{statuses[message.status] ?? "Desconhecido"}</span></td>
                <td data-label="Tentativas">{message.attempts}</td>
                <td data-label="Falha" className="muted">{message.failureCode || "—"}</td>
                <td data-label="Ação">{message.status === 4 && (role <= 1 || role === 3) ? <form action={retryMessage.bind(null, message.id)}><button className="btn btn-secondary" type="submit">Reenviar</button></form> : message.status === 5 && role <= 1 ? <ReconciliationForm messageId={message.id} /> : "—"}</td>
              </tr>
            )}</tbody>
          </table>
        )}
      </section>
    </>
  );
}
