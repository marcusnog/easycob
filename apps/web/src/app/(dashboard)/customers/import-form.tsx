"use client";

import { useActionState } from "react";
import SubmitButton from "@/components/submit-button";
import { importCustomers } from "./import-actions";

export default function ImportForm() {
  const [state, action] = useActionState(importCustomers, null);
  return (
    <form className="card form" action={action}>
      <h2>Importar clientes</h2>
      <label>Arquivo CSV
        <input name="file" type="file" accept=".csv,text/csv" required />
      </label>
      <small className="muted">Cabeçalhos: Name, Document, Phone, Email e WhatsAppOptIn. Limite de 10 MiB.</small>
      {state?.error && <p className="error" role="alert">{state.error}</p>}
      {state?.ok && <p className="success" role="status">{state.ok}</p>}
      <SubmitButton>Importar CSV</SubmitButton>
    </form>
  );
}
