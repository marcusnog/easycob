import Link from "next/link";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import NavLinks from "./nav-links";

function LogoMark() {
  return (
    <span className="brand-mark" aria-hidden="true">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
        <path d="M12 3v18" />
        <path d="M16.5 8.5c0-1.7-1.8-2.75-4.5-2.75S7.5 6.8 7.5 8.5c0 3.5 9 1.75 9 5.25 0 1.7-1.8 2.75-4.5 2.75S7.5 15.45 7.5 13.75" />
      </svg>
    </span>
  );
}

export default async function DashboardLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const session = await cookies();
  if (!session.has("access_token")) redirect(session.has("refresh_token") ? "/api/auth/refresh" : "/login");

  return (
    <div className="shell">
      <aside className="sidebar">
        <Link className="brand" href="/dashboard">
          <LogoMark />
          <span>EasyCob</span>
        </Link>
        <nav className="side-nav" aria-label="Navegação principal">
          <NavLinks />
        </nav>
        <div className="sidebar-foot">
          <a className="btn btn-ghost" href="/api/auth/logout">Sair</a>
        </div>
      </aside>
      <header className="topbar">
        <Link className="brand" href="/dashboard">
          <LogoMark />
          <span>EasyCob</span>
        </Link>
        <a className="btn btn-secondary" href="/api/auth/logout">Sair</a>
      </header>
      <nav className="nav" aria-label="Navegação principal">
        <NavLinks />
      </nav>
      <main className="content">{children}</main>
    </div>
  );
}
