import Link from "next/link";
import { api } from "@/lib/api";

type AuditEvent = { id: string; actorId: string; action: string; entityType: string; entityId: string; occurredAt: string };

const actionLabels: Record<string, string> = {
  "charge.cancelled": "Cobrança cancelada",
  "charge.created": "Cobrança criada",
  "contact.created": "Contato criado",
  "contact.opted-in": "WhatsApp autorizado",
  "contact.opted-out": "WhatsApp revogado",
  "customer.anonymized": "Cliente anonimizado",
  "customer.archived": "Cliente arquivado",
  "customer.created": "Cliente criado",
  "customer.updated": "Cliente atualizado",
  "customers.imported": "Clientes importados",
  "message.delivery-reconciled": "Entrega reconciliada",
  "message-template.created": "Template criado",
  "message.retry-requested": "Reenvio solicitado",
  "payment.recorded": "Pagamento registrado",
  "collection-rule.activated": "Regra ativada",
  "collection-rule.created": "Regra criada",
  "collection-rule.deactivated": "Regra desativada",
  "tenant.settings-updated": "Configurações alteradas",
};

export default async function AuditPage({ searchParams }: { searchParams: Promise<{ page?: string }> }) {
  const requestedPage = Number((await searchParams).page ?? 1);
  const page = Number.isInteger(requestedPage) && requestedPage > 0 ? requestedPage : 1;
  let events: AuditEvent[] = [];
  let timeZone = "America/Sao_Paulo";
  let failed = false;
  try {
    const result = await Promise.all([api<AuditEvent[]>(`/audit-events?page=${page}`), api<{ timeZone: string }>("/tenant/")]);
    events = result[0];
    timeZone = result[1].timeZone;
  } catch { failed = true; }

  return (
    <>
      <div className="page-header">
        <div>
          <p className="eyebrow">Segurança e rastreabilidade</p>
          <h1>Auditoria</h1>
          <p className="muted">Consulte as ações sensíveis realizadas na empresa.</p>
        </div>
      </div>
      <section className="card table-wrap" aria-labelledby="audit-list">
        <div className="table-card-header">
          <h2 id="audit-list">Eventos recentes</h2>
          {!failed && <span className="pill">Página {page}</span>}
        </div>
        {failed ? <p className="error" role="alert">Não foi possível carregar a auditoria. O acesso é restrito a Owner e Admin.</p> : events.length === 0 ? (
          <p className="empty"><strong>Nenhum evento nesta página</strong>As ações sensíveis aparecerão aqui.</p>
        ) : <table>
          <thead><tr><th>Data e hora</th><th>Ação</th><th>Recurso</th><th>Responsável</th></tr></thead>
          <tbody>{events.map(event => <tr key={event.id}>
            <td data-label="Data e hora"><time dateTime={event.occurredAt}>{new Date(event.occurredAt).toLocaleString("pt-BR", { timeZone })}</time></td>
            <td data-label="Ação">{actionLabels[event.action] ?? event.action}</td>
            <td data-label="Recurso"><span title={event.entityId}>{event.entityType} · {event.entityId.slice(0, 8)}</span></td>
            <td data-label="Responsável"><span title={event.actorId}>{event.actorId.slice(0, 18)}{event.actorId.length > 18 ? "…" : ""}</span></td>
          </tr>)}</tbody>
        </table>}
        {!failed && <nav className="pagination" aria-label="Paginação da auditoria">
          {page > 1 && <Link className="btn btn-secondary" href={`/audit?page=${page - 1}`}>Anterior</Link>}
          {events.length === 100 && <Link className="btn btn-secondary" href={`/audit?page=${page + 1}`}>Próxima</Link>}
        </nav>}
      </section>
    </>
  );
}
