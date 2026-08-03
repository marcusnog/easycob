import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: { default: "EasyCob", template: "%s | EasyCob" },
  description: "Cobranças simples, acompanhamento claro.",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="pt-BR">
      <body>{children}</body>
    </html>
  );
}
