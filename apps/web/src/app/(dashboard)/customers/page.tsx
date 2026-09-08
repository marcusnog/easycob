import { revalidatePath } from "next/cache";
import { api } from "@/lib/api";
import SubmitButton from "@/components/submit-button";
import Link from "next/link";
import ContactForm from "./contact-form";
import { setConsent } from "./contact-actions";
import ImportForm from "./import-form";

type Customer = { id: string; name: string; document?: string; createdAt: string };
type Contact = { id: string; phone?: string; email?: string; whatsAppOptIn: boolean; consentAt?: string; optOutAt?: string };
type CustomerDetail = Customer & { contacts: Contact[] };
type CurrentUser = { role: number };

async function createCustomer(formData: FormData) {
  "use server";
  await api("/customers", { method: "POST", body: JSON.stringify({ name: formData.get("name"), document: formData.get("document") || null }) });
  revalidatePath("/customers");
  revalidatePath("/charges");
}

export default async function CustomersPage({ searchParams }: { searchParams: Promise<{ customer?: string; search?: string }> }) {
  const params = await searchParams;
  let customers: Customer[] = [];
  let currentUser: CurrentUser | null = null;
  let failed = false;
  const query = params.search?.trim();
  try { [customers, currentUser] = await Promise.all([api<Customer[]>(`/customers${query ? `?search=${encodeURIComponent(query)}` : ""}`), api<CurrentUser>("/tenant/me")]); } catch { failed = true; }
  let selected: CustomerDetail | null = null;
  if (params.customer && /^[\da-f-]{36}$/i.test(params.customer)) {
    try { selected = await api<CustomerDetail>(`/customers/${params.customer}`); } catch { /* Exibe a falha abaixo. */ }
  }

  return (
    <>
      <div className="page-header">
        <div>
          <p className="eyebrow">Base de contatos</p>
          <h1>Clientes</h1>
          <p className="muted">Cadastre e consulte seus clientes para emitir cobranças.</p>
        </div>
      </div>
      {currentUser && currentUser.role <= 2 && <div style={{ marginBottom: "1.5rem" }}><ImportForm /></div>}
      {params.customer && !selected && <p className="error" role="alert">Não foi possível carregar os contatos. Selecione o cliente novamente.</p>}
      {selected && <section className="card" style={{ marginBottom: "1.5rem" }} aria-labelledby="contact-title">
        <div className="table-card-header">
          <h2 id="contact-title">Contatos — {selected.name}</h2>
          <Link className="btn btn-secondary" href="/customers">Fechar</Link>
        </div>
        <div className="grid split">
          <ContactForm customerId={selected.id} />
          <div>
            <h2>Contatos cadastrados</h2>
            {selected.contacts.length === 0 ? <p className="muted">Nenhum contato cadastrado.</p> : <ul className="settings-list">
              {selected.contacts.map(contact => <li key={contact.id}>
                <span>{contact.phone || contact.email}</span>
                <form action={setConsent.bind(null, selected.id, contact.id, !contact.whatsAppOptIn)}>
                  <button className={`btn ${contact.whatsAppOptIn ? "btn-secondary" : "btn-primary"}`} type="submit">
                    {contact.whatsAppOptIn ? "Revogar WhatsApp" : "Autorizar WhatsApp"}
                  </button>
                </form>
              </li>)}
            </ul>}
          </div>
        </div>
      </section>}
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
          <form action="/customers" className="search-form">
            <label>Buscar por nome ou documento
              <input name="search" defaultValue={query} placeholder="Digite para buscar" />
            </label>
            <button className="btn btn-secondary" type="submit">Buscar</button>
          </form>
          {failed ? (
            <p className="error" role="alert">Não foi possível carregar os clientes.</p>
          ) : customers.length === 0 ? (
            <p className="empty"><strong>Nenhum cliente cadastrado</strong>Cadastre um cliente ao lado para começar a emitir cobranças.</p>
          ) : (
            <table>
              <thead><tr><th>Nome</th><th>Documento</th><th>Cadastro</th><th>Ações</th></tr></thead>
              <tbody>{customers.map(customer =>
                  <tr key={customer.id}><td data-label="Nome">{customer.name}</td><td data-label="Documento">{customer.document || "—"}</td><td data-label="Cadastro">{new Date(customer.createdAt).toLocaleDateString("pt-BR")}</td><td data-label="Ações"><div className="actions"><Link className="btn btn-secondary" href={`/customers?customer=${customer.id}`}>Ver contatos</Link>{currentUser && currentUser.role <= 1 && <a className="btn btn-secondary" href={`/api/customers/${customer.id}/export`}>Exportar</a>}</div></td></tr>
              )}</tbody>
            </table>
          )}
        </section>
      </div>
    </>
  );
}
