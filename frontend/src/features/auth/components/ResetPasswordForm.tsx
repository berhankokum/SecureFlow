import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Link, useNavigate } from "react-router-dom";
import { useResetPassword } from "../hooks/useResetPassword";
import { resetPasswordSchema, type ResetPasswordFormValues } from "../schemas/authSchemas";
import { ApiError } from "@/lib/api/apiClient";

interface ResetPasswordFormProps {
  email: string;
  code: string;
}

export function ResetPasswordForm({ email, code }: ResetPasswordFormProps) {
  const [formError, setFormError] = useState<string | null>(null);
  const resetPasswordMutation = useResetPassword();
  const navigate = useNavigate();

  const {
    register: field,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordFormValues>({ resolver: zodResolver(resetPasswordSchema) });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await resetPasswordMutation.mutateAsync({ email, code, newPassword: values.newPassword });
      navigate("/login", { replace: true });
    } catch (error) {
      setFormError(
        error instanceof ApiError ? error.message : "Şifre sıfırlanamadı veya bağlantının süresi dolmuş.",
      );
    }
  });

  return (
    <form onSubmit={onSubmit} className="space-y-4" noValidate>
      {formError ? (
        <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{formError}</p>
      ) : null}

      <div>
        <label htmlFor="newPassword" className="block text-sm font-medium text-slate-700">
          Yeni şifre
        </label>
        <input
          id="newPassword"
          type="password"
          autoComplete="new-password"
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
          {...field("newPassword")}
        />
        {errors.newPassword ? (
          <p className="mt-1 text-sm text-red-600">{errors.newPassword.message}</p>
        ) : null}
      </div>

      <button
        type="submit"
        disabled={isSubmitting}
        className="w-full rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white disabled:opacity-50"
      >
        {isSubmitting ? "Kaydediliyor..." : "Şifreyi sıfırla"}
      </button>

      <p className="text-center text-sm text-slate-600">
        <Link to="/login" className="underline">
          Girişe dön
        </Link>
      </p>
    </form>
  );
}
