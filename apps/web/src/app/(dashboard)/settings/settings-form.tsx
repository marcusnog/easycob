"use client";

import { useActionState } from "react";
import { updateSettings } from "./actions";
import SubmitButton from "@/components/submit-button";

export type TenantSettings = { name: string; timeZone: string; currency: string; whatsAppPhoneNumberId?: string | null };

const timeZones: [string, string][] = [
  ["America/Sao_Paulo", "Brasília (UTC-3)"],
  ["America/Fortaleza", "Fortaleza (UTC-3)"],
  ["America/Recife", "Recife (UTC-3)"],
  ["America/Belem", "Belém (UTC-3)"],
  ["America/Manaus", "Manaus (UTC-4)"],
  ["America/Cuiaba", "Cuiabá (UTC-4)"],
  ["America/Porto_Velho", "Porto Velho (UTC-4)"],
  ["America/New_York", "Nova York (UTC-5)"],
  ["America/Los_Angeles", "Los Angeles (UTC-8)"],
  ["America/Mexico_City", "Cidade do México (UTC-6)"],
  ["America/Bogota", "Bogotá (UTC-5)"],
  ["America/Buenos_Aires", "Buenos Aires (UTC-3)"],
  ["Europe/Lisbon", "Lisboa (UTC+0)"],
  ["Europe/London", "Londres (UTC+0)"],
  ["UTC", "UTC"],
];

const currencies = ["BRL", "USD", "EUR", "GBP", "ARS", "UYU", "CLP", "COP", "MXN", "PYG"];

export default function SettingsForm({ tenant }: { tenant: TenantSettings }) {
  const [state, formAction] = useActionState(updateSettings, null);

  return (
    <form className="card form" action={formAction}>
      <h2>Empresa</h2>
      {state?.ok && <p className="success" role="status">{state.ok}</p>}
      {state?.error && <p className="error" role="alert">{state.error}</p>}
      <label>Nome da empresa
        <input name="name" required maxLength={160} defaultValue={tenant.name} placeholder="Ex.: Minha Empresa Ltda" />
      </label>
      <label>Fuso horário
        <select name="timeZone" required defaultValue={tenant.timeZone}>
          {timeZones.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select>
      </label>
      <label>Moeda
        <select name="currency" required defaultValue={tenant.currency}>
          {currencies.map(currency => <option key={currency} value={currency}>{currency}</option>)}
        </select>
      </label>
      <label>WhatsApp phone number ID
        <input name="whatsAppPhoneNumberId" maxLength={32} defaultValue={tenant.whatsAppPhoneNumberId ?? ""} placeholder="Ex.: 123456789012345" />
        <small>Identificador numérico do número na Meta WhatsApp Cloud API.</small>
      </label>
      <SubmitButton>Salvar configurações</SubmitButton>
    </form>
  );
}
