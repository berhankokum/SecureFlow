import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ApiError } from "@/lib/api/apiClient";
import { createWorkItemSchema, updateWorkItemSchema, type WorkItemFormValues } from "../schemas/workItemSchema";

interface WorkItemFormProps {
  mode: "create" | "update";
  initialValues?: WorkItemFormValues;
  onSubmit: (values: WorkItemFormValues) => Promise<void>;
  onCancel: () => void;
}

export function WorkItemForm({ mode, initialValues, onSubmit, onCancel }: WorkItemFormProps) {
  const [formError, setFormError] = useState<string | null>(null);
  const { register, handleSubmit, setError, formState: { errors, isSubmitting } } = useForm<WorkItemFormValues>({
    resolver: zodResolver(mode === "create" ? createWorkItemSchema : updateWorkItemSchema),
    defaultValues: initialValues ?? { type: "TASK", title: "", description: "", priority: "MEDIUM", dueDate: "" },
  });

  const submit = handleSubmit(async values => {
    setFormError(null);
    try {
      await onSubmit(values);
    } catch (error) {
      if (error instanceof ApiError && error.errors) {
        for (const [field, messages] of Object.entries(error.errors)) {
          setError(field.toLowerCase() as keyof WorkItemFormValues, { message: messages[0] });
        }
        return;
      }
      setFormError(error instanceof ApiError ? error.message : "WorkItem işlemi tamamlanamadı.");
    }
  });

  return <form onSubmit={submit} className="space-y-4" noValidate>
    <h3 className="text-lg font-semibold text-slate-900">{mode === "create" ? "Yeni WorkItem" : "WorkItem düzenle"}</h3>
    {formError ? <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{formError}</p> : null}
    <div className="grid gap-4 sm:grid-cols-2">
      <div><label htmlFor={`${mode}-workitem-type`} className="block text-sm font-medium text-slate-700">Tür</label><select id={`${mode}-workitem-type`} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm" {...register("type")}><option value="TASK">TASK</option><option value="BUG">BUG</option><option value="STORY">STORY</option></select>{errors.type ? <p className="mt-1 text-sm text-red-600">{errors.type.message}</p> : null}</div>
      <div><label htmlFor={`${mode}-workitem-priority`} className="block text-sm font-medium text-slate-700">Öncelik</label><select id={`${mode}-workitem-priority`} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm" {...register("priority")}><option value="LOW">LOW</option><option value="MEDIUM">MEDIUM</option><option value="HIGH">HIGH</option><option value="CRITICAL">CRITICAL</option></select>{errors.priority ? <p className="mt-1 text-sm text-red-600">{errors.priority.message}</p> : null}</div>
    </div>
    <div><label htmlFor={`${mode}-workitem-title`} className="block text-sm font-medium text-slate-700">Başlık</label><input id={`${mode}-workitem-title`} autoFocus maxLength={200} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm" {...register("title")} />{errors.title ? <p className="mt-1 text-sm text-red-600">{errors.title.message}</p> : null}</div>
    <div><label htmlFor={`${mode}-workitem-description`} className="block text-sm font-medium text-slate-700">Açıklama <span className="font-normal text-slate-500">(isteğe bağlı)</span></label><textarea id={`${mode}-workitem-description`} rows={5} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm" {...register("description")} />{errors.description ? <p className="mt-1 text-sm text-red-600">{errors.description.message}</p> : null}</div>
    <div><label htmlFor={`${mode}-workitem-due-date`} className="block text-sm font-medium text-slate-700">Due date <span className="font-normal text-slate-500">(isteğe bağlı)</span></label><input id={`${mode}-workitem-due-date`} type="date" className="mt-1 rounded-md border border-slate-300 px-3 py-2 text-sm" {...register("dueDate")} />{errors.dueDate ? <p className="mt-1 text-sm text-red-600">{errors.dueDate.message}</p> : null}</div>
    <div className="flex justify-end gap-3"><button type="button" onClick={onCancel} className="rounded-md border border-slate-300 px-4 py-2 text-sm">Vazgeç</button><button type="submit" disabled={isSubmitting} className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">{isSubmitting ? "Kaydediliyor..." : mode === "create" ? "WorkItem oluştur" : "Kaydet"}</button></div>
  </form>;
}
