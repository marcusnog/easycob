"use server";

import { revalidatePath } from "next/cache";
import { api } from "@/lib/api";

export type SettingsState = { ok?: string; error?: string } | null;

export async function updateSettings(_prev: SettingsState, formData: FormData): Promise<SettingsState> {
  const name = String(formData.get("name") ?? "").trim();
  const timeZone = String(formData.get("timeZone") ?? "").trim();
  const currency = String(formData.get("currency") ?? "").trim();
  const whatsAppPhoneNumberId = String(formData.get("whatsAppPhoneNumberId") ?? "").trim();

  if (!name) return { error: "Informe o nome da empresa." };
  if (!timeZone) return { error: "Selecione o fuso horário." };
  if (!currency) return { error: "Selecione a moeda." };

  try {
    await api("/tenant/settings", {
      method: "PUT",
      body: JSON.stringify({ name, timeZone, currency, whatsAppPhoneNumberId: whatsAppPhoneNumberId || null }),
    });
  } catch {
    return { error: "Não foi possível salvar. Verifique as permissões e tente novamente." };
  }

  revalidatePath("/settings");
  return { ok: "Configurações salvas." };
}
