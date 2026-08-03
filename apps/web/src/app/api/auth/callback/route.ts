import { originOf, required } from "@/lib/auth";
import { NextRequest, NextResponse } from "next/server";

export async function GET(request: NextRequest) {
  const code = request.nextUrl.searchParams.get("code");
  const state = request.nextUrl.searchParams.get("state");
  const expectedState = request.cookies.get("oauth_state")?.value;
  const verifier = request.cookies.get("pkce_verifier")?.value;
  const origin = originOf(request);
  if (!code || !state || state !== expectedState || !verifier) return NextResponse.redirect(new URL("/login?error=oauth", `${origin}/`));

  const callback = `${origin}/api/auth/callback`;
  const tokenResponse = await fetch(`${required("COGNITO_DOMAIN").replace(/\/$/, "")}/oauth2/token`, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({ grant_type: "authorization_code", client_id: required("COGNITO_CLIENT_ID"), code, redirect_uri: callback, code_verifier: verifier }),
  });
  if (!tokenResponse.ok) return NextResponse.redirect(new URL("/login?error=token", `${origin}/`));
  const token = (await tokenResponse.json()) as { access_token: string; refresh_token?: string; expires_in: number };
  const response = NextResponse.redirect(new URL("/dashboard", `${origin}/`));
  const cookie = { httpOnly: true, sameSite: "lax" as const, secure: origin.startsWith("https://"), path: "/" };
  response.cookies.set("access_token", token.access_token, { ...cookie, maxAge: token.expires_in });
  if (token.refresh_token) response.cookies.set("refresh_token", token.refresh_token, { ...cookie, maxAge: 30 * 24 * 60 * 60 });
  response.cookies.delete("oauth_state");
  response.cookies.delete("pkce_verifier");
  return response;
}
