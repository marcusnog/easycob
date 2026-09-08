import { revalidatePath } from "next/cache";
import { api, money } from "@/lib/api";
import SubmitButton from "@/components/submit-button";
import Link from "next/link";
import { randomUUID } from "node:crypto";
import PaymentForm from "./payment-form";

type Customer = { id: string; name: string };
type Charge = { id: string; customerId: string; description: string; amount: number; dueDate: string; status: number };
type ChargeDetail = Charge & { payments: { id: string; amount: number; paidAt: string }[] };
const statuses = ["Aberta", "Em atraso", "Parcial", "Paga", "Cancelada"];
const pillClass = ["info", "bad", "warn", "ok", "warn"];

async function createCharge(formData: FormData) {
  "use server";
  await api("/charges", { method: "POST", body: JSON.stringify({ customerId: formData.get("customerId"), description: formData.get("description"), amount: Number(formData.get("amount")), firstDueDate: formData.get("firstDueDate"), installments: Number(formData.get("installments")) }) });
  revalidatePath("/charges");
  revalidatePath("/dashboard");
}

export default async function ChargesPage({ searchParams }: { searchParams: Promise<{ charge?: string }> }) {
  let charges: Charge[] = [], customers: Customer[] = [];
  let failed = false;
  try { [charges, customers] = await Promise.all([api<Charge[]>("/charges"), api<Customer[]>("/customers")]); } catch { failed = true; }
  const byId = new Map(customers.map(customer => [customer.id, customer.name]));
  const { charge: selectedId } = await searchParams;
  let selected: ChargeDetail | null = null;
  if (selectedId && /^[\da-f-]{36}$/i.test(selectedId)) {
    try { selected = await api<ChargeDetail>(`/charges/${selectedId}`); } catch { /* A mensagem abaixo permite tentar novamente. */ }
  }
  const paid = selected ? selected.payments.reduce((sum, payment) => sum + Math.round(payment.amount * 100), 0) / 100 : 0;
  const balance = selected ? (Math.round(selected.amount * 100) - Math.round(paid * 100)) / 100 : 0;

  return (
    <>
      <div className="page-header">
        <div>
          <p className="eyebrow">Carteira de títulos</p>
          <h1>Cobranças</h1>
          <p className="muted">Crie cobranças à vista ou parceladas e acompanhe cada vencimento.</p>
        </div>
      </div>
      {selectedId && !selected && <p className="error" role="alert">Não foi possível carregar os pagamentos. Selecione a cobrança novamente.</p>}
      {selected && <section className="card form" id="payments" aria-labelledby="payment-title" style={{ marginBottom: "1.5rem" }}>
        <div className="table-card-header">
          <h2 id="payment-title">Pagamentos — {selected.description}</h2>
          <Link className="btn btn-secondary" href="/charges">Fechar</Link>
        </div>
        <p>Total: {money(selected.amount)} · Recebido: {money(paid)} · Saldo: {money(balance)}</p>
        <p><span className={`pill ${pillClass[selected.status] ?? ""}`}>{statuses[selected.status]}</span></p>
        {selected.status !== 3 && selected.status !== 4 && balance > 0 && <PaymentForm key={selected.id} chargeId={selected.id} balance={balance} externalId={randomUUID()} />}
        {selected.status === 3 && <p className="success" role="status">Cobrança quitada. Baixa concluída.</p>}
        <h2>Histórico de pagamentos</h2>
        {selected.payments.length === 0 ? <p className="muted">Nenhum pagamento registrado.</p> : <ul className="settings-list">
          {selected.payments.map(payment => <li key={payment.id}><time dateTime={payment.paidAt}>{new Date(payment.paidAt).toLocaleString("pt-BR", { timeZone: "America/Sao_Paulo" })} (Brasília)</time><strong>{money(payment.amount)}</strong></li>)}
        </ul>}
      </section>}
      <div className="grid split">
        <form className="card form" action={createCharge}>
          <h2>Nova cobrança</h2>
          <label>Cliente
            <select name="customerId" required defaultValue=""><option value="" disabled>Selecione</option>{customers.map(customer => <option key={customer.id} value={customer.id}>{customer.name}</option>)}</select>
          </label>
          <label>Descrição
            <input name="description" required maxLength={200} placeholder="Ex.: Fatura de julho" />
          </label>
          <label>Valor
            <input name="amount" type="number" min="0.01" step="0.01" required placeholder="0,00" />
          </label>
          <label>Primeiro vencimento
            <input name="firstDueDate" type="date" required />
          </label>
          <label>Parcelas
            <input name="installments" type="number" min="1" max="120" defaultValue="1" required />
          </label>
          <SubmitButton disabled={customers.length === 0}>Criar cobrança</SubmitButton>
          {customers.length === 0 && <p className="muted error" role="alert">Cadastre um cliente antes de criar cobranças.</p>}
        </form>
        <section className="card table-wrap" aria-labelledby="charge-list">
          <div className="table-card-header">
            <h2 id="charge-list">Cobranças recentes</h2>
            {!failed && <span className="pill">{charges.length} título{charges.length === 1 ? "" : "s"}</span>}
          </div>
          {failed ? (
            <p className="error" role="alert">Não foi possível carregar as cobranças.</p>
          ) : charges.length === 0 ? (
            <p className="empty"><strong>Nenhuma cobrança cadastrada</strong>Crie a primeira cobrança ao lado para começar a acompanhar seus vencimentos.</p>
          ) : (
            <table>
              <thead><tr><th>Descrição</th><th>Cliente</th><th>Valor</th><th>Vencimento</th><th>Status</th><th>Ações</th></tr></thead>
              <tbody>{charges.map(charge =>
                <tr key={charge.id}>
                  <td data-label="Descrição">{charge.description}</td>
                  <td data-label="Cliente" className="muted">{byId.get(charge.customerId) ?? "—"}</td>
                  <td data-label="Valor">{money(charge.amount)}</td>
                  <td data-label="Vencimento">{new Date(`${charge.dueDate}T00:00:00`).toLocaleDateString("pt-BR")}</td>
                  <td data-label="Status"><span className={`pill ${pillClass[charge.status] ?? ""}`}>{statuses[charge.status] ?? "Desconhecido"}</span></td>
                  <td data-label="Ações"><Link className="btn btn-secondary" href={`/charges?charge=${charge.id}#payments`}>{charge.status === 3 || charge.status === 4 ? "Ver pagamentos" : "Registrar pagamento"}</Link></td>
                </tr>
              )}</tbody>
            </table>
          )}
        </section>
      </div>
    </>
  );
}
