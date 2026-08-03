"use client";

import { useFormStatus } from "react-dom";

export default function SubmitButton({ children, disabled, ...rest }: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  const { pending } = useFormStatus();
  return (
    <button className="btn btn-primary" type="submit" disabled={disabled || pending} {...rest}>
      {pending && <span className="spinner" aria-hidden="true" />}
      <span>{children}</span>
    </button>
  );
}
