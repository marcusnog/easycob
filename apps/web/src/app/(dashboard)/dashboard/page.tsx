import { api, money } from "@/lib/api";

type Summary = { receivable: number; overdue: number; received: number };

export default async function DashboardPage() {
  let summary: Summary | null = null;
  try { summary = await api<Summary>("/finance/summary"); } catch { /* shown below */ }

  return (
    <>
      <div className="page-header">
        <div>
          <p className="eyebrow">Painel financeiro</p>
          <h1>Visão geral</h1>
          <p className="muted">Acompanhe sua operação de cobrança em tempo real.</p>
        </div>
        <a className="btn btn-secondary" href="/charges">Nova cobrança</a>
      </div>
      {!summary ? (
        <p className="card error" role="alert">Não foi possível carregar os dados. Confira se a API está disponível.</p>
      ) : (
        <section className="grid cards" aria-label="Resumo financeiro">
          <article className="metric-card tone-brand">
            <div className="metric-top">
              <span className="icon-chip">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M21 12a2.25 2.25 0 0 0-2.25-2.25H15a3 3 0 1 1-6 0H5.25A2.25 2.25 0 0 0 3 12m18 0v6a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 18v-6m18 0V9M3 12V9m18 0a2.25 2.25 0 0 0-2.25-2.25H5.25A2.25 2.25 0 0 0 3 9m18 0V6a2.25 2.25 0 0 0-2.25-2.25H5.25A2.25 2.25 0 0 0 3 6v3" />
                </svg>
              </span>
              <span className="pill">Em aberto</span>
            </div>
            <p className="metric-label">A receber</p>
            <p className="metric-value">{money(summary.receivable)}</p>
            <p className="metric-foot">Soma dos títulos ainda não quitados</p>
          </article>
          <article className="metric-card tone-danger">
            <div className="metric-top">
              <span className="icon-chip">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
                </svg>
              </span>
              <span className="pill bad">Precisa de atenção</span>
            </div>
            <p className="metric-label">Em atraso</p>
            <p className="metric-value">{money(summary.overdue)}</p>
            <p className="metric-foot">Valores vencidos e não pagos</p>
          </article>
          <article className="metric-card tone-ok">
            <div className="metric-top">
              <span className="icon-chip">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
                </svg>
              </span>
              <span className="pill ok">Quitado</span>
            </div>
            <p className="metric-label">Recebido</p>
            <p className="metric-value">{money(summary.received)}</p>
            <p className="metric-foot">Total recebido até o momento</p>
          </article>
        </section>
      )}
    </>
  );
}
