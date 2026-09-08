"use client";

import { useActionState } from "react";
import SubmitButton from "@/components/submit-button";
import { recordPayment, type PaymentState } from "./payment-actions";

export default function PaymentForm({ chargeId, balance, externalId }: { chargeId: string; balance: number; externalId: string }) {
  const [state, action, pending] = useActionState(async (previous: PaymentState, data: FormData) => {
    const date = new Date(String(data.get("paidAt") ?? ""));
    data.set("paidAt", Number.isFinite(date.getTime()) ? date.toISOString() : "");
    return recordPayment(chargeId, previous, data);
  }, null);
  return (
    <form className="form" action={action}>
      <input type="hidden" name="externalId" value={externalId} />
      <label>Valor recebido (R$)
        <input key={balance} name="amount" type="number" min="0.01" max={balance.toFixed(2)} step="0.01" defaultValue={balance.toFixed(2)} required disabled={pending} />
      </label>
      <label>Data e hora do pagamento
        <input name="paidAt" type="datetime-local" required disabled={pending} />
      </label>
      <p className="muted">Informe um valor parcial ou mantenha o saldo total para dar baixa. Use seu horário local.</p>
      {state?.error && <p className="error" role="alert">{state.error}</p>}
      {state?.ok && <p className="success" role="status">{state.ok}</p>}
      <SubmitButton disabled={pending}>{pending ? "Registrando…" : "Confirmar pagamento"}</SubmitButton>
    </form>
  );
}
