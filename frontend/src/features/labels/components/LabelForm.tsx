import { useId, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ApiError } from "@/lib/api/apiClient";
import { labelSchema, type LabelFormValues } from "../schemas/labelSchema";

export function LabelForm({ initialName = "", onSubmit, onCancel }: {
  initialName?: string; onSubmit: (name: string) => Promise<void>; onCancel: () => void;
}) {
  const id = useId();
  const [error, setError] = useState<string | null>(null);
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<LabelFormValues>({
    resolver: zodResolver(labelSchema), defaultValues: { name: initialName },
  });

  return <form noValidate className="mt-4 space-y-3 rounded-lg border border-slate-200 bg-white p-4" onSubmit={handleSubmit(async values => {
    setError(null);
    try { await onSubmit(values.name); }
    catch (cause) { setError(cause instanceof ApiError ? cause.message : "Label kaydedilemedi."); }
  })}>
    <label htmlFor={id} className="block text-sm font-medium text-slate-700">Label adı</label>
    <input id={id} autoFocus aria-invalid={!!errors.name} aria-describedby={errors.name ? `${id}-error` : undefined} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" {...register("name")} />
    {errors.name ? <p id={`${id}-error`} role="alert" className="text-sm text-red-700">{errors.name.message}</p> : null}
    {error ? <p role="alert" className="text-sm text-red-700">{error}</p> : null}
    <div className="flex gap-3"><button type="submit" disabled={isSubmitting} className="rounded-md bg-slate-900 px-4 py-2 text-sm text-white disabled:opacity-50">{isSubmitting ? "Kaydediliyor..." : "Label kaydet"}</button><button type="button" disabled={isSubmitting} onClick={onCancel} className="text-sm text-slate-600">Vazgeç</button></div>
  </form>;
}
