import { api } from "@/lib/api";

type Message = { id: string; chargeId?: string; status: number; scheduledAt: string; sentAt?: string; attempts: number; failureCode?: string };
const statuses = ["Pendente", "Enviada", "Entregue", "Lida", "Falhou", "Enviando"];
const pillClass = ["warn", "info", "ok", "ok", "bad", "info"];

export default async function MessagesPage() {
  let messages: Message[] = [];
  let failed = false;
  try { messages = await api<Message[]>("/messages"); } catch { failed = true; }

  return (
    <>
      <div className="page-header">
        <div>
          <p className="eyebrow">Régua de cobrança</p>
          <h1>Mensagens</h1>
          <p className="muted">Acompanhe as cobranças enviadas pelo WhatsApp e o estado de cada envio.</p>
        </div>
      </div>
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
            <thead><tr><th>Agendamento</th><th>Status</th><th>Tentativas</th><th>Falha</th></tr></thead>
            <tbody>{messages.map(message =>
              <tr key={message.id}>
                <td data-label="Agendamento">{new Date(message.scheduledAt).toLocaleString("pt-BR")}</td>
                <td data-label="Status"><span className={`pill ${pillClass[message.status] ?? ""}`}>{statuses[message.status] ?? "Desconhecido"}</span></td>
                <td data-label="Tentativas">{message.attempts}</td>
                <td data-label="Falha" className="muted">{message.failureCode || "—"}</td>
              </tr>
            )}</tbody>
          </table>
        )}
      </section>
    </>
  );
}
