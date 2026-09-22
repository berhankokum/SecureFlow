import { useId, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Link } from "react-router-dom";
import { useForgotPassword } from "../hooks/useForgotPassword";
import { forgotPasswordSchema, type ForgotPasswordFormValues } from "../schemas/authSchemas";

export function ForgotPasswordForm() {
  const errorId = useId();
  const [submitted, setSubmitted] = useState(false);
  const forgotPasswordMutation = useForgotPassword();

  const {
    register: field,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ForgotPasswordFormValues>({ resolver: zodResolver(forgotPasswordSchema) });

  const onSubmit = handleSubmit(async (values) => {
    await forgotPasswordMutation.mutateAsync(values);
    // Backend hesabın var/yok olduğunu sızdırmaz: her koşulda aynı sonuç gösterilir.
    setSubmitted(true);
  });

  if (submitted) {
    return (
      <div className="rounded-md border border-slate-200 bg-white p-4 text-sm text-slate-700">
        Eğer bu e-posta ile bir hesap varsa, şifre sıfırlama bağlantısı gönderildi.
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} className="space-y-4" noValidate>
      <div>
        <label htmlFor="email" className="block text-sm font-medium text-slate-700">
          E-posta
        </label>
        <input
          id="email"
          type="email"
          autoComplete="email"
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
          aria-invalid={Boolean(errors.email)} aria-describedby={errors.email ? `${errorId}-email` : undefined} {...field("email")}
        />
        {errors.email ? <p id={`${errorId}-email`} role="alert" className="mt-1 text-sm text-red-600">{errors.email.message}</p> : null}
      </div>

      <button
        type="submit"
        disabled={isSubmitting}
        className="w-full rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white disabled:opacity-50"
      >
        {isSubmitting ? "Gönderiliyor..." : "Sıfırlama bağlantısı gönder"}
      </button>

      <p className="text-center text-sm text-slate-600">
        <Link to="/login" className="underline">
          Girişe dön
        </Link>
      </p>
    </form>
  );
}
