import { NextResponse } from "next/server";

const attempts = new Map<string, { count: number; resetAt: number }>();

export function rateLimit(request: Request, scope: string, limit = 20, windowMs = 60_000) {
  const forwarded = request.headers.get("x-forwarded-for")?.split(",").at(-1)?.trim();
  const key = `${scope}:${forwarded || "unknown"}`;
  const now = Date.now();
  const current = attempts.get(key);
  if (!current || current.resetAt <= now) {
    // ponytail: memória local serve ao container único; migrar para WAF ao escalar horizontalmente.
    if (!current && attempts.size >= 10_000) attempts.delete(attempts.keys().next().value!);
    attempts.set(key, { count: 1, resetAt: now + windowMs });
    return null;
  }
  current.count++;
  if (current.count <= limit) return null;
  return NextResponse.json({ error: "Muitas tentativas. Aguarde e tente novamente." }, {
    status: 429,
    headers: { "Retry-After": Math.ceil((current.resetAt - now) / 1000).toString() },
  });
}
