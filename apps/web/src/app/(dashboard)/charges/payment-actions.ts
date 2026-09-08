"use server";

import { revalidatePath } from "next/cache";
import { api } from "@/lib/api";

export type PaymentState = { error?: string; ok?: string } | null;

export async function recordPayment(chargeId: string, _previous: PaymentState, formData: FormData): Promise<PaymentState> {
  const amount = String(formData.get("amount") ?? "");
  const externalId = String(formData.get("externalId") ?? "");
  if (!/^[\da-f-]{36}$/i.test(chargeId) || !/^[\da-f-]{36}$/i.test(externalId)) return { error: "Reabra a cobrança e tente novamente." };
  if (!/^\d+(\.\d{1,2})?$/.test(amount) || !Number.isFinite(Number(amount)) || Number(amount) <= 0) return { error: "Informe um valor positivo com até duas casas decimais." };
  try {
    await api(`/charges/${chargeId}/payments`, { method: "POST", body: JSON.stringify({ amount: Number(amount), externalId }) });
  } catch (error) {
    if (error instanceof Error && error.message === "API 403") return { error: "Apenas Owner, Admin e Finance podem registrar pagamentos." };
    if (error instanceof Error && error.message === "API 400") return { error: "Confira o valor: o pagamento não pode exceder o saldo atual." };
    if (error instanceof Error && error.message === "API 409") return { error: "Pagamento já registrado ou cobrança cancelada. Atualize a página para conferir." };
    return { error: "Não foi possível confirmar o pagamento. Confira o histórico antes de tentar novamente." };
  }
  revalidatePath("/charges");
  revalidatePath("/dashboard");
  return { ok: "Pagamento registrado. Saldo e situação atualizados." };
}
