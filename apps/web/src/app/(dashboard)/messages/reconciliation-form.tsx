"use client";

import { useActionState } from "react";
import { resolveMessage } from "./actions";

export default function ReconciliationForm({ messageId }: { messageId: string }) {
  const [state, action, pending] = useActionState(resolveMessage.bind(null, messageId), null);
  return <form className="form compact-form" action={action}>
    <label>ID da mensagem na Meta
      <input name="externalId" maxLength={512} placeholder="Ex.: wamid..." disabled={pending} />
    </label>
    {state?.error && <p className="error" role="alert">{state.error}</p>}
    {state?.ok && <p className="success" role="status">{state.ok}</p>}
    <div className="actions">
      <button className="btn btn-primary" type="submit" name="result" value="sent" disabled={pending}>Confirmar envio</button>
      <button className="btn btn-secondary" type="submit" name="result" value="failed" disabled={pending}>Marcar falha</button>
    </div>
  </form>;
}
