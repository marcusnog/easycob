"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { api } from "@/lib/api";

export type LifecycleState = { error?: string; ok?: string } | null;

export async function updateCustomer(id: string, _previous: LifecycleState, formData: FormData): Promise<LifecycleState> {
  const name = String(formData.get("name") ?? "").trim();
  const document = String(formData.get("document") ?? "").trim();
  if (!name) return { error: "Informe o nome do cliente." };
  try {
    await api(`/customers/${id}`, { method: "PUT", body: JSON.stringify({ name, document: document || null }) });
  } catch (error) {
    if (error instanceof Error && error.message === "API 409") return { error: "Já existe outro cliente com este documento." };
    return { error: "Não foi possível atualizar o cliente." };
  }
  revalidatePath("/customers");
  revalidatePath("/charges");
  return { ok: "Cadastro atualizado." };
}

export async function archiveCustomer(id: string) {
  await api(`/customers/${id}`, { method: "DELETE" });
  revalidatePath("/customers");
  revalidatePath("/charges");
  redirect("/customers");
}

export async function anonymizeCustomer(id: string, _previous: LifecycleState, formData: FormData): Promise<LifecycleState> {
  if (formData.get("confirm") !== "on") return { error: "Confirme que deseja remover permanentemente os dados pessoais." };
  try {
    await api(`/customers/${id}/anonymize`, { method: "POST" });
  } catch (error) {
    if (error instanceof Error && error.message === "API 409") return { error: "O cliente possui cobrança em aberto. Quite ou cancele antes de anonimizar." };
    return { error: "Não foi possível anonimizar o cliente." };
  }
  revalidatePath("/customers");
  revalidatePath("/charges");
  redirect("/customers");
}
