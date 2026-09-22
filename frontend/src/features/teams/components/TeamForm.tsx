import { useId, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ApiError } from "@/lib/api/apiClient";
import { teamSchema, type TeamFormValues } from "../schemas/teamSchema";

interface TeamFormProps {
  title: string;
  submitLabel: string;
  initialValues?: TeamFormValues;
  onSubmit: (values: TeamFormValues) => Promise<void>;
  onCancel?: () => void;
}

export function TeamForm({ title, submitLabel, initialValues, onSubmit, onCancel }: TeamFormProps) {
  const errorId = useId();
  const [formError, setFormError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<TeamFormValues>({
    resolver: zodResolver(teamSchema),
    defaultValues: initialValues ?? { name: "", description: "" },
  });

  const submit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await onSubmit(values);
    } catch (error) {
      if (error instanceof ApiError && error.errors) {
        for (const [field, messages] of Object.entries(error.errors)) {
          setError(field.toLowerCase() as keyof TeamFormValues, { message: messages[0] });
        }
        return;
      }
      setFormError(error instanceof ApiError ? error.message : "Team işlemi tamamlanamadı.");
    }
  });

  return (
    <form onSubmit={submit} className="space-y-4" noValidate>
      <h2 className="text-lg font-semibold text-slate-900">{title}</h2>
      {formError ? <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{formError}</p> : null}
      <div>
        <label htmlFor="team-name" className="block text-sm font-medium text-slate-700">Team adı</label>
        <input id="team-name" autoFocus className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm" aria-invalid={Boolean(errors.name)} aria-describedby={errors.name ? `${errorId}-name` : undefined} {...register("name")} />
        {errors.name ? <p id={`${errorId}-name`} role="alert" className="mt-1 text-sm text-red-600">{errors.name.message}</p> : null}
      </div>
      <div>
        <label htmlFor="team-description" className="block text-sm font-medium text-slate-700">Açıklama <span className="font-normal text-slate-500">(isteğe bağlı)</span></label>
        <textarea id="team-description" rows={4} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm" aria-invalid={Boolean(errors.description)} aria-describedby={errors.description ? `${errorId}-description` : undefined} {...register("description")} />
        {errors.description ? <p id={`${errorId}-description`} role="alert" className="mt-1 text-sm text-red-600">{errors.description.message}</p> : null}
      </div>
      <div className="flex justify-end gap-3">
        {onCancel ? <button type="button" onClick={onCancel} className="rounded-md border border-slate-300 px-4 py-2 text-sm">Vazgeç</button> : null}
        <button type="submit" disabled={isSubmitting} className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">
          {isSubmitting ? "Kaydediliyor..." : submitLabel}
        </button>
      </div>
    </form>
  );
}
