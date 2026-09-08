"use server";

import { revalidatePath } from "next/cache";
import { api } from "@/lib/api";

export type ContactState = { error?: string; ok?: string } | null;

export async function createContact(customerId: string, _previous: ContactState, formData: FormData): Promise<ContactState> {
  const phone = String(formData.get("phone") ?? "").trim();
  const email = String(formData.get("email") ?? "").trim();
  if (!phone && !email) return { error: "Informe um telefone ou e-mail." };
  try {
    await api(`/customers/${customerId}/contacts`, {
      method: "POST",
      body: JSON.stringify({ phone: phone || null, email: email || null, whatsAppOptIn: formData.get("whatsAppOptIn") === "on" }),
    });
  } catch (error) {
    if (error instanceof Error && error.message === "API 403") return { error: "Você não tem permissão para cadastrar contatos." };
    if (error instanceof Error && error.message === "API 400") return { error: "Confira o telefone: use DDD e entre 10 e 15 dígitos." };
    return { error: "Não foi possível cadastrar o contato." };
  }
  revalidatePath("/customers");
  return { ok: "Contato cadastrado." };
}

export async function setConsent(customerId: string, contactId: string, optIn: boolean) {
  await api(`/customers/${customerId}/contacts/${contactId}/consent`, {
    method: "PUT",
    body: JSON.stringify({ optIn }),
  });
  revalidatePath("/customers");
}
