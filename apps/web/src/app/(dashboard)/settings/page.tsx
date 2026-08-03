import { api } from "@/lib/api";
import SettingsForm, { type TenantSettings } from "./settings-form";

type Me = { id: string; email: string; role: string };
type User = { id: string; email: string; role: string; active: boolean };

const rolePill: Record<string, string> = { Owner: "warn", Admin: "info", Finance: "ok", Collector: "info", Viewer: "warn" };
const roleLabel: Record<string, string> = { Owner: "Dono", Admin: "Administrador", Finance: "Financeiro", Collector: "Cobrador", Viewer: "Consulta" };

export default async function SettingsPage() {
  let tenant: TenantSettings | null = null;
  let me: Me | null = null;
  let users: User[] = [];
  let failed = false;
  try {
    [tenant, me] = await Promise.all([api<TenantSettings>("/tenant"), api<Me>("/tenant/me")]);
    if (me.role === "Owner" || me.role === "Admin") users = await api<User[]>("/tenant/users");
  } catch {
    failed = true;
  }
  const canEdit = !!me && (me.role === "Owner" || me.role === "Admin");

  return (
    <>
      <div className="page-header">
        <div>
          <p className="eyebrow">Tenant</p>
          <h1>Configurações</h1>
          <p className="muted">Dados da empresa, fuso horário, moeda e usuários do espaço de trabalho.</p>
        </div>
      </div>
      {failed ? (
        <p className="error" role="alert">Não foi possível carregar as configurações.</p>
      ) : canEdit ? (
        <div className="grid split">
          <SettingsForm tenant={tenant!} />
          <section className="card table-wrap" aria-labelledby="user-list">
            <div className="table-card-header">
              <h2 id="user-list">Usuários</h2>
              <span className="pill">{users.length} usuário{users.length === 1 ? "" : "s"}</span>
            </div>
            {users.length === 0 ? (
              <p className="empty"><strong>Nenhum usuário</strong>Os usuários são criados no primeiro acesso pelo Cognito.</p>
            ) : (
              <table>
                <thead><tr><th>Email</th><th>Papel</th><th>Status</th></tr></thead>
                <tbody>{users.map(user =>
                  <tr key={user.id}>
                    <td data-label="Email">{user.email}</td>
                    <td data-label="Papel"><span className={`pill ${rolePill[user.role] ?? ""}`}>{roleLabel[user.role] ?? user.role}</span></td>
                    <td data-label="Status"><span className={`pill ${user.active ? "ok" : "bad"}`}>{user.active ? "Ativo" : "Inativo"}</span></td>
                  </tr>
                )}</tbody>
              </table>
            )}
          </section>
        </div>
      ) : (
        <section className="card">
          <h2>Empresa</h2>
          <ul className="settings-list">
            <li><span>Nome</span><strong>{tenant!.name}</strong></li>
            <li><span>Fuso horário</span><strong>{tenant!.timeZone}</strong></li>
            <li><span>Moeda</span><strong>{tenant!.currency}</strong></li>
            <li><span>WhatsApp phone number ID</span><strong>{tenant!.whatsAppPhoneNumberId || "—"}</strong></li>
          </ul>
          <p className="muted">Somente Owner e Admin podem alterar as configurações do tenant.</p>
        </section>
      )}
    </>
  );
}
