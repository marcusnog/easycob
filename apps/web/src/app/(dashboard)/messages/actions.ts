"use server";

import { revalidatePath } from "next/cache";
import { api } from "@/lib/api";

export type ActionState = { error?: string; ok?: string } | null;

export async function createTemplate(_previous: ActionState, formData: FormData): Promise<ActionState> {
  const name = String(formData.get("name") ?? "").trim();
  const metaTemplateId = String(formData.get("metaTemplateId") ?? "").trim();
  const language = String(formData.get("language") ?? "").trim();
  if (!name || !metaTemplateId || !language) return { error: "Preencha todos os campos do template." };
  try {
    await api("/message-templates", { method: "POST", body: JSON.stringify({ name, metaTemplateId, language }) });
  } catch { return { error: "Não foi possível criar o template. Verifique sua permissão e os dados." }; }
  revalidatePath("/messages");
  return { ok: "Nova versão do template criada." };
}

export async function createRule(_previous: ActionState, formData: FormData): Promise<ActionState> {
  const name = String(formData.get("name") ?? "").trim();
  const messageTemplateId = String(formData.get("messageTemplateId") ?? "");
  const daysOffset = Number(formData.get("daysOffset"));
  if (!name || !/^[\da-f-]{36}$/i.test(messageTemplateId) || !Number.isInteger(daysOffset) || daysOffset < -365 || daysOffset > 365)
    return { error: "Informe nome, template e intervalo entre -365 e 365 dias." };
  try {
    await api("/collection-rules", { method: "POST", body: JSON.stringify({ name, messageTemplateId, daysOffset }) });
  } catch { return { error: "Não foi possível criar a regra. Verifique sua permissão e os dados." }; }
  revalidatePath("/messages");
  return { ok: "Regra criada e ativada." };
}

export async function setRuleActive(id: string, active: boolean) {
  await api(`/collection-rules/${id}/active`, { method: "PUT", body: JSON.stringify({ active }) });
  revalidatePath("/messages");
}

export async function retryMessage(id: string) {
  await api(`/messages/${id}/retry`, { method: "POST" });
  revalidatePath("/messages");
}

export async function resolveMessage(id: string, _previous: ActionState, formData: FormData): Promise<ActionState> {
  const sent = formData.get("result") === "sent";
  const externalId = String(formData.get("externalId") ?? "").trim();
  if (sent && !externalId) return { error: "Informe o ID da mensagem retornado pela Meta." };
  try {
    await api(`/messages/${id}/resolve`, { method: "POST", body: JSON.stringify({ sent, externalId: externalId || null }) });
  } catch { return { error: "Não foi possível reconciliar a mensagem. Atualize a página e tente novamente." }; }
  revalidatePath("/messages");
  return { ok: sent ? "Envio confirmado." : "Mensagem marcada como falha." };
}
