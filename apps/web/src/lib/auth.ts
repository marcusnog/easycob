import { createHash, randomBytes } from "node:crypto";

export function createAuthorizationRequest(origin: string) {
  const domain = required("COGNITO_DOMAIN").replace(/\/$/, "");
  const clientId = required("COGNITO_CLIENT_ID");
  const state = randomBytes(24).toString("base64url");
  const verifier = randomBytes(48).toString("base64url");
  const challenge = createHash("sha256").update(verifier).digest("base64url");
  const callback = `${origin}/api/auth/callback`;
  const query = new URLSearchParams({ response_type: "code", client_id: clientId, redirect_uri: callback, scope: "openid email", state, code_challenge: challenge, code_challenge_method: "S256" });
  return { url: `${domain}/oauth2/authorize?${query}`, state, verifier };
}

export function originOf(request: Request) {
  const proto = request.headers.get("x-forwarded-proto") ?? "http";
  return `${proto}://${request.headers.get("host")}`;
}

export function required(name: string) {
  const value = process.env[name];
  if (!value || value.includes("CHANGE_ME")) throw new Error(`${name} não configurado`);
  return value;
}

export async function refreshTokens(refreshToken: string) {
  const response = await fetch(`${required("COGNITO_DOMAIN").replace(/\/$/, "")}/oauth2/token`, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({ grant_type: "refresh_token", client_id: required("COGNITO_CLIENT_ID"), refresh_token: refreshToken }),
    cache: "no-store",
  });
  if (!response.ok) throw new Error("refresh_failed");
  return response.json() as Promise<{ access_token: string; expires_in: number }>;
}
