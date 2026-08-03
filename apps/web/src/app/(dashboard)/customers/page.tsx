import { revalidatePath } from "next/cache";
import { api } from "@/lib/api";
import SubmitButton from "@/components/submit-button";

type Customer = { id: string; name: string; document?: string; createdAt: string };

async function createCustomer(formData: FormData) {
  "use server";
  await api("/customers", { method: "POST", body: JSON.stringify({ name: formData.get("name"), document: formData.get("document") || null }) });
  revalidatePath("/customers");
  revalidatePath("/charges");
}

export default async function CustomersPage() {
  let customers: Customer[] = [];
  let failed = false;
  try { customers = await api<Customer[]>("/customers"); } catch { failed = true; }

  return (
    <>
      <div className="page-header">
        <div>
          <p className="eyebrow">Base de contatos</p>
          <h1>Clientes</h1>
          <p className="muted">Cadastre e consulte seus clientes para emitir cobranças.</p>
        </div>
      </div>
      <div className="grid split">
        <form className="card form" action={createCustomer}>
          <h2>Novo cliente</h2>
          <label>Nome
            <input name="name" required maxLength={160} autoComplete="name" placeholder="Ex.: Maria Silva" />
          </label>
          <label>CPF ou CNPJ
            <input name="document" maxLength={30} inputMode="numeric" placeholder="Somente números" />
            <small>Opcional — facilita a localização do cliente.</small>
          </label>
          <SubmitButton>Cadastrar cliente</SubmitButton>
        </form>
        <section className="card table-wrap" aria-labelledby="customer-list">
          <div className="table-card-header">
            <h2 id="customer-list">Clientes cadastrados</h2>
            {!failed && <span className="pill">{customers.length} cadastrado{customers.length === 1 ? "" : "s"}</span>}
          </div>
          {failed ? (
            <p className="error" role="alert">Não foi possível carregar os clientes.</p>
          ) : customers.length === 0 ? (
            <p className="empty"><strong>Nenhum cliente cadastrado</strong>Cadastre um cliente ao lado para começar a emitir cobranças.</p>
          ) : (
            <table>
              <thead><tr><th>Nome</th><th>Documento</th><th>Cadastro</th></tr></thead>
              <tbody>{customers.map(customer =>
                  <tr key={customer.id}><td data-label="Nome">{customer.name}</td><td data-label="Documento">{customer.document || "—"}</td><td data-label="Cadastro">{new Date(customer.createdAt).toLocaleDateString("pt-BR")}</td></tr>
              )}</tbody>
            </table>
          )}
        </section>
      </div>
    </>
  );
}
