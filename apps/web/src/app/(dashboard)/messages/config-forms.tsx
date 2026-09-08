"use client";

import { useActionState } from "react";
import SubmitButton from "@/components/submit-button";
import { createRule, createTemplate } from "./actions";

type Template = { id: string; name: string; version: number };

export default function ConfigForms({ templates }: { templates: Template[] }) {
  const [templateState, templateAction] = useActionState(createTemplate, null);
  const [ruleState, ruleAction] = useActionState(createRule, null);
  return <div className="grid split">
    <form className="card form" action={templateAction}>
      <h2>Novo template</h2>
      <label>Nome interno<input name="name" required maxLength={120} placeholder="Ex.: lembrete_vencimento" /></label>
      <label>Nome aprovado na Meta<input name="metaTemplateId" required maxLength={512} placeholder="Ex.: cobranca_vencendo" /></label>
      <label>Idioma<input name="language" required maxLength={16} defaultValue="pt_BR" /></label>
      <small className="muted">Repetir o nome interno cria uma nova versão.</small>
      {templateState?.error && <p className="error" role="alert">{templateState.error}</p>}
      {templateState?.ok && <p className="success" role="status">{templateState.ok}</p>}
      <SubmitButton>Criar template</SubmitButton>
    </form>
    <form className="card form" action={ruleAction}>
      <h2>Nova regra</h2>
      <label>Nome<input name="name" required maxLength={120} placeholder="Ex.: Aviso 3 dias antes" /></label>
      <label>Template<select name="messageTemplateId" required defaultValue=""><option value="" disabled>Selecione</option>{templates.map(template => <option key={template.id} value={template.id}>{template.name} · v{template.version}</option>)}</select></label>
      <label>Dias em relação ao vencimento<input name="daysOffset" type="number" min="-365" max="365" defaultValue="-3" required /></label>
      <small className="muted">Use número negativo para avisar antes e positivo para cobrar depois do vencimento.</small>
      {ruleState?.error && <p className="error" role="alert">{ruleState.error}</p>}
      {ruleState?.ok && <p className="success" role="status">{ruleState.ok}</p>}
      <SubmitButton disabled={templates.length === 0}>Criar regra</SubmitButton>
    </form>
  </div>;
}
