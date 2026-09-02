import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ApiError } from "@/lib/api/apiClient";
import { createProjectSchema, updateProjectSchema, type CreateProjectFormValues, type UpdateProjectFormValues } from "../schemas/projectSchema";

type Values = CreateProjectFormValues | UpdateProjectFormValues;

interface ProjectFormProps {
  mode: "create" | "update";
  initialValues?: UpdateProjectFormValues;
  onSubmit: (values: Values) => Promise<void>;
  onCancel: () => void;
}

export function ProjectForm({ mode, initialValues, onSubmit, onCancel }: ProjectFormProps) {
  const [formError, setFormError] = useState<string | null>(null);
  const { register, handleSubmit, setError, formState: { errors, isSubmitting } } = useForm<Values>({
    resolver: zodResolver(mode === "create" ? createProjectSchema : updateProjectSchema),
    defaultValues: mode === "create"
      ? { key: "", name: "", description: "" }
      : initialValues ?? { name: "", description: "" },
  });

  const submit = handleSubmit(async values => {
    setFormError(null);
    try { await onSubmit(values); } catch (error) {
      if (error instanceof ApiError && error.errors) {
        for (const [field, messages] of Object.entries(error.errors)) {
          setError(field.toLowerCase() as keyof Values, { message: messages[0] });
        }
        return;
      }
      if (mode === "create" && error instanceof ApiError && error.message.includes("Project key")) {
        setError("key", { message: error.message });
        return;
      }
      setFormError(error instanceof ApiError ? error.message : "Project işlemi tamamlanamadı.");
    }
  });

  return (
    <form onSubmit={submit} className="space-y-4" noValidate>
      <h2 className="text-lg font-semibold text-slate-900">{mode === "create" ? "Yeni Project" : "Project ayarları"}</h2>
      {formError ? <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{formError}</p> : null}
      {mode === "create" ? <div>
        <label htmlFor="project-key" className="block text-sm font-medium text-slate-700">Project key</label>
        <input id="project-key" autoFocus maxLength={10} placeholder="SF" className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm uppercase" {...register("key")} />
        {"key" in errors && errors.key ? <p className="mt-1 text-sm text-red-600">{errors.key.message}</p> : null}
      </div> : null}
      <div>
        <label htmlFor="project-name" className="block text-sm font-medium text-slate-700">Project adı</label>
        <input id="project-name" className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm" {...register("name")} />
        {errors.name ? <p className="mt-1 text-sm text-red-600">{errors.name.message}</p> : null}
      </div>
      <div>
        <label htmlFor="project-description" className="block text-sm font-medium text-slate-700">Açıklama <span className="font-normal text-slate-500">(isteğe bağlı)</span></label>
        <textarea id="project-description" rows={4} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm" {...register("description")} />
        {errors.description ? <p className="mt-1 text-sm text-red-600">{errors.description.message}</p> : null}
      </div>
      <div className="flex justify-end gap-3">
        <button type="button" onClick={onCancel} className="rounded-md border border-slate-300 px-4 py-2 text-sm">Vazgeç</button>
        <button type="submit" disabled={isSubmitting} className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">{isSubmitting ? "Kaydediliyor..." : mode === "create" ? "Project oluştur" : "Kaydet"}</button>
      </div>
    </form>
  );
}
