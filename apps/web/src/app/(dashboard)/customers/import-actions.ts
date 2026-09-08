"use server";

import { revalidatePath } from "next/cache";
import { api } from "@/lib/api";

export type ImportState = { error?: string; ok?: string } | null;

export async function importCustomers(_previous: ImportState, formData: FormData): Promise<ImportState> {
  const file = formData.get("file");
  if (!(file instanceof File) || file.size === 0) return { error: "Selecione um arquivo CSV." };
  if (file.size > 10_485_760) return { error: "O arquivo deve ter no máximo 10 MiB." };
  const body = new FormData();
  body.set("file", file);
  try {
    const result = await api<{ created: number; skipped: number }>("/customers/import", { method: "POST", body });
    revalidatePath("/customers");
    return { ok: `${result.created} cliente(s) importado(s); ${result.skipped} duplicado(s) ignorado(s).` };
  } catch (error) {
    if (error instanceof Error && error.message === "API 403") return { error: "Você não tem permissão para importar clientes." };
    if (error instanceof Error && error.message === "API 400") return { error: "CSV inválido. Confira os cabeçalhos e os dados informados." };
    return { error: "Não foi possível importar o arquivo." };
  }
}
