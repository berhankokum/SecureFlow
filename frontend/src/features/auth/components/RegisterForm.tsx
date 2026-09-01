import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Link } from "react-router-dom";
import { useRegister } from "../hooks/useRegister";
import { registerSchema, type RegisterFormValues } from "../schemas/authSchemas";
import { ApiError } from "@/lib/api/apiClient";

export function RegisterForm() {
  const [submitted, setSubmitted] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const registerMutation = useRegister();

  const {
    register: field,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormValues>({ resolver: zodResolver(registerSchema) });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await registerMutation.mutateAsync(values);
      setSubmitted(true);
    } catch (error) {
      if (error instanceof ApiError && error.errors) {
        for (const [fieldName, messages] of Object.entries(error.errors)) {
          setError(fieldName.toLowerCase() as keyof RegisterFormValues, { message: messages[0] });
        }
        return;
      }
      setFormError(error instanceof ApiError ? error.message : "Kayıt sırasında bir hata oluştu.");
    }
  });

  if (submitted) {
    return (
      <div className="rounded-md border border-slate-200 bg-white p-4 text-sm text-slate-700">
        Hesabınız oluşturuldu. E-postanıza gönderilen bağlantı ile hesabınızı doğrulayabilirsiniz.
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} className="space-y-4" noValidate>
      {formError ? (
        <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{formError}</p>
      ) : null}

      <div>
        <label htmlFor="email" className="block text-sm font-medium text-slate-700">
          E-posta
        </label>
        <input
          id="email"
          type="email"
          autoComplete="email"
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
          {...field("email")}
        />
        {errors.email ? <p className="mt-1 text-sm text-red-600">{errors.email.message}</p> : null}
      </div>

      <div>
        <label htmlFor="password" className="block text-sm font-medium text-slate-700">
          Şifre
        </label>
        <input
          id="password"
          type="password"
          autoComplete="new-password"
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
          {...field("password")}
        />
        {errors.password ? <p className="mt-1 text-sm text-red-600">{errors.password.message}</p> : null}
      </div>

      <button
        type="submit"
        disabled={isSubmitting}
        className="w-full rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white disabled:opacity-50"
      >
        {isSubmitting ? "Kayıt olunuyor..." : "Kayıt Ol"}
      </button>

      <p className="text-center text-sm text-slate-600">
        Zaten hesabın var mı? <Link to="/login" className="text-slate-900 underline">Giriş yap</Link>
      </p>
    </form>
  );
}
