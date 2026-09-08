"use client";

import { useActionState } from "react";
import SubmitButton from "@/components/submit-button";
import { createContact } from "./contact-actions";

export default function ContactForm({ customerId }: { customerId: string }) {
  const [state, action] = useActionState(createContact.bind(null, customerId), null);
  return (
    <form className="form" action={action}>
      <h2>Novo contato</h2>
      <label>WhatsApp / telefone
        <input name="phone" type="tel" inputMode="tel" autoComplete="tel" maxLength={20} placeholder="Ex.: 11999998888" />
      </label>
      <label>E-mail
        <input name="email" type="email" autoComplete="email" maxLength={254} placeholder="cliente@empresa.com" />
      </label>
      <label className="check"><input name="whatsAppOptIn" type="checkbox" /> Cliente autorizou mensagens pelo WhatsApp</label>
      <small className="muted">Marque somente quando houver consentimento do cliente.</small>
      {state?.error && <p className="error" role="alert">{state.error}</p>}
      {state?.ok && <p className="success" role="status">{state.ok}</p>}
      <SubmitButton>Cadastrar contato</SubmitButton>
    </form>
  );
}
