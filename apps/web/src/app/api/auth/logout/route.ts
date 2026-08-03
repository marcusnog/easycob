import { NextRequest, NextResponse } from "next/server";
import { originOf, required } from "@/lib/auth";

export function GET(request: NextRequest) {
  const query = new URLSearchParams({ client_id: required("COGNITO_CLIENT_ID"), logout_uri: `${originOf(request)}/login` });
  const response = NextResponse.redirect(`${required("COGNITO_DOMAIN").replace(/\/$/, "")}/logout?${query}`);
  for (const name of ["access_token", "refresh_token"]) response.cookies.delete(name);
  return response;
}
