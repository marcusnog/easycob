import { revalidatePath } from "next/cache";
import { api, money } from "@/lib/api";
import SubmitButton from "@/components/submit-button";

type Customer = { id: string; name: string };
type Charge = { id: string; customerId: string; description: string; amount: number; dueDate: string; status: number };
const statuses = ["Aberta", "Em atraso", "Parcial", "Paga", "Cancelada"];
const pillClass = ["info", "bad", "warn", "ok", "warn"];

async function createCharge(formData: FormData) {
  "use server";
  await api("/charges", { method: "POST", body: JSON.stringify({ customerId: formData.get("customerId"), description: formData.get("description"), amount: Number(formData.get("amount")), firstDueDate: formData.get("firstDueDate"), installments: Number(formData.get("installments")) }) });
  revalidatePath("/charges");
  revalidatePath("/dashboard");
}

export default async function ChargesPage() {
  let charges: Charge[] = [], customers: Customer[] = [];
  let failed = false;
  try { [charges, customers] = await Promise.all([api<Charge[]>("/charges"), api<Customer[]>("/customers")]); } catch { failed = true; }
  const byId = new Map(customers.map(customer => [customer.id, customer.name]));

  return (
    <>
      <div className="page-header">
        <div>
          <p className="eyebrow">Carteira de títulos</p>
          <h1>Cobranças</h1>
          <p className="muted">Crie cobranças à vista ou parceladas e acompanhe cada vencimento.</p>
        </div>
      </div>
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
              <thead><tr><th>Descrição</th><th>Cliente</th><th>Valor</th><th>Vencimento</th><th>Status</th></tr></thead>
              <tbody>{charges.map(charge =>
                <tr key={charge.id}>
                  <td data-label="Descrição">{charge.description}</td>
                  <td data-label="Cliente" className="muted">{byId.get(charge.customerId) ?? "—"}</td>
                  <td data-label="Valor">{money(charge.amount)}</td>
                  <td data-label="Vencimento">{new Date(`${charge.dueDate}T00:00:00`).toLocaleDateString("pt-BR")}</td>
                  <td data-label="Status"><span className={`pill ${pillClass[charge.status] ?? ""}`}>{statuses[charge.status] ?? "Desconhecido"}</span></td>
                </tr>
              )}</tbody>
            </table>
          )}
        </section>
      </div>
    </>
  );
}
