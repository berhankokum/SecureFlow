import { useState } from "react";
import { permissions } from "@/features/authorization/permissions";
import type { ProjectDetails } from "@/features/projects/api/types";
import { ApiError } from "@/lib/api/apiClient";
import type { Label } from "../api/labelApi";
import { useCreateLabel, useDeleteLabel, useLabels, useUpdateLabel } from "../hooks/useLabels";
import { LabelForm } from "./LabelForm";

export function ProjectLabelsSection({ project }: { project: ProjectDetails }) {
  const query = useLabels(project.id);
  const create = useCreateLabel(project.id);
  const update = useUpdateLabel(project.id);
  const remove = useDeleteLabel(project.id);
  const [editing, setEditing] = useState<Label | "new" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const active = project.archivedAt === null;
  const canCreate = active && project.permissions.includes(permissions.labelCreate);
  const canUpdate = active && project.permissions.includes(permissions.labelUpdate);
  const canDelete = active && project.permissions.includes(permissions.labelDelete);

  return <section aria-labelledby="project-labels-title" className="mt-8">
    <div className="flex items-center justify-between gap-3"><div><h2 id="project-labels-title" className="text-lg font-semibold text-slate-900">Labels</h2><p className="mt-1 text-sm text-slate-600">Bu Project’in WorkItem’larında kullanılabilecek etiketler.</p></div>{canCreate ? <button type="button" onClick={() => setEditing("new")} className="rounded-md bg-slate-900 px-4 py-2 text-sm text-white">Label oluştur</button> : null}</div>
    {editing && (editing === "new" ? canCreate : canUpdate) ? <LabelForm key={editing === "new" ? "new" : editing.id} initialName={editing === "new" ? "" : editing.name} onCancel={() => setEditing(null)} onSubmit={async name => {
      if (editing === "new") await create.mutateAsync(name);
      else await update.mutateAsync({ id: editing.id, name });
      setEditing(null);
    }} /> : null}
    {query.isLoading ? <p className="mt-4 text-sm text-slate-500">Labels yükleniyor...</p> : null}
    {query.isError ? <p role="alert" className="mt-4 text-sm text-red-700">Labels yüklenemedi.</p> : null}
    {error ? <p role="alert" className="mt-4 text-sm text-red-700">{error}</p> : null}
    {query.data?.length === 0 ? <p className="mt-4 rounded-lg border border-dashed border-slate-300 p-5 text-sm text-slate-500">Henüz Label yok.</p> : null}
    <ul className="mt-4 divide-y divide-slate-100 rounded-lg border border-slate-200 bg-white">{query.data?.map(label => <li key={label.id} className="flex items-center justify-between gap-3 px-4 py-3">
      <span className="break-all rounded bg-slate-100 px-2 py-1 text-sm text-slate-700">{label.name}</span>
      <div className="flex shrink-0 gap-3">{canUpdate ? <button type="button" aria-label={`${label.name} Label düzenle`} onClick={() => setEditing(label)} className="text-sm text-slate-600 hover:underline">Düzenle</button> : null}{canDelete ? <button type="button" aria-label={`${label.name} Label sil`} disabled={remove.isPending} className="text-sm text-red-700 hover:underline disabled:opacity-50" onClick={async () => {
        if (!window.confirm(`“${label.name}” silinsin mi? WorkItem’lardaki bu Label ilişkileri de kaldırılır.`)) return;
        setError(null);
        try { await remove.mutateAsync(label.id); if (editing !== "new" && editing?.id === label.id) setEditing(null); }
        catch (cause) { setError(cause instanceof ApiError ? cause.message : "Label silinemedi."); }
      }}>Sil</button> : null}</div>
    </li>)}</ul>
  </section>;
}
