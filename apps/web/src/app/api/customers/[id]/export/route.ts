import { apiResponse } from "@/lib/api";

export async function GET(_request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!/^[\da-f-]{36}$/i.test(id)) return new Response("Cliente inválido.", { status: 400 });
  const response = await apiResponse(`/customers/${id}/export`);
  if (!response.ok) return new Response(response.status === 403 ? "Sem permissão para exportar." : "Cliente não encontrado.", { status: response.status });
  return new Response(response.body, {
    headers: {
      "Content-Type": "application/json; charset=utf-8",
      "Content-Disposition": `attachment; filename="cliente-${id}.json"`,
    },
  });
}
