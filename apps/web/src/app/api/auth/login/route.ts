import { createAuthorizationRequest, originOf } from "@/lib/auth";
import { NextRequest, NextResponse } from "next/server";
import { rateLimit } from "@/lib/rate-limit";

export async function GET(request: NextRequest) {
  const limited = rateLimit(request, "login");
  if (limited) return limited;
  const auth = createAuthorizationRequest(originOf(request));
  const response = NextResponse.redirect(auth.url);
  const secure = originOf(request).startsWith("https://");
  response.cookies.set("oauth_state", auth.state, { httpOnly: true, sameSite: "lax", secure, maxAge: 600, path: "/" });
  response.cookies.set("pkce_verifier", auth.verifier, { httpOnly: true, sameSite: "lax", secure, maxAge: 600, path: "/" });
  return response;
}
