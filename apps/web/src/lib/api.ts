import { cookies } from "next/headers";

const apiUrl = process.env.API_URL ?? "http://localhost:5000";

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await apiResponse(path, init);
  if (!response.ok) throw new Error(`API ${response.status}`);
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}

export async function apiResponse(path: string, init?: RequestInit) {
  const token = (await cookies()).get("access_token")?.value;
  if (!token) throw new Error("unauthenticated");
  const headers = new Headers(init?.headers);
  headers.set("Authorization", `Bearer ${token}`);
  if (init?.body && !(init.body instanceof FormData) && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  return fetch(`${apiUrl}${path}`, {
    ...init,
    cache: "no-store",
    headers,
  });
}

export function money(value: number) {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(value);
}
