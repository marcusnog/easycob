"use client";

import { useActionState } from "react";
import SubmitButton from "@/components/submit-button";
import { anonymizeCustomer, archiveCustomer, updateCustomer } from "./lifecycle-actions";

export default function LifecycleForm({ customer, role }: { customer: { id: string; name: string; document?: string }; role: number }) {
  const [updateState, updateAction] = useActionState(updateCustomer.bind(null, customer.id), null);
  const [anonymizeState, anonymizeAction] = useActionState(anonymizeCustomer.bind(null, customer.id), null);
  return <div className="grid split">
    <form className="form" action={updateAction}>
      <h2>Dados do cliente</h2>
      <label>Nome<input name="name" required maxLength={160} defaultValue={customer.name} /></label>
      <label>CPF ou CNPJ<input name="document" maxLength={30} inputMode="numeric" defaultValue={customer.document ?? ""} /></label>
      {updateState?.error && <p className="error" role="alert">{updateState.error}</p>}
      {updateState?.ok && <p className="success" role="status">{updateState.ok}</p>}
      <SubmitButton>Salvar alterações</SubmitButton>
    </form>
    {role <= 1 && <div className="form danger-zone">
      <h2>Privacidade e encerramento</h2>
      <form className="form" action={archiveCustomer.bind(null, customer.id)}>
        <label className="check"><input type="checkbox" required /> Confirmo que desejo arquivar este cliente</label>
        <button className="btn btn-secondary" type="submit">Arquivar cliente</button>
      </form>
      <form className="form" action={anonymizeAction}>
        <label className="check"><input name="confirm" type="checkbox" required /> Confirmo a remoção permanente dos dados pessoais</label>
        <small className="muted">A anonimização só é permitida quando não existem cobranças abertas.</small>
        {anonymizeState?.error && <p className="error" role="alert">{anonymizeState.error}</p>}
        <button className="btn btn-danger" type="submit">Anonimizar cliente</button>
      </form>
    </div>}
  </div>;
}
