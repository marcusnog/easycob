import { originOf, refreshTokens } from "@/lib/auth";
import { NextRequest, NextResponse } from "next/server";

export async function GET(request: NextRequest) {
  const refreshToken = request.cookies.get("refresh_token")?.value;
  const requestedReturn = request.nextUrl.searchParams.get("returnTo") ?? "/dashboard";
  const returnTo = requestedReturn.startsWith("/") && !requestedReturn.startsWith("//") ? requestedReturn : "/dashboard";
  if (!refreshToken) return NextResponse.redirect(new URL("/login?error=session", request.url));
  try {
    const token = await refreshTokens(refreshToken);
    const response = NextResponse.redirect(new URL(returnTo, `${originOf(request)}/`));
    const cookie = { httpOnly: true, sameSite: "lax" as const, secure: originOf(request).startsWith("https://"), path: "/" };
    response.cookies.set("access_token", token.access_token, { ...cookie, maxAge: token.expires_in });
    return response;
  } catch {
    const response = NextResponse.redirect(new URL("/login?error=session", request.url));
    for (const name of ["access_token", "refresh_token"]) response.cookies.delete(name);
    return response;
  }
}
