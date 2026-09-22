import { LoadingState } from "@/components/ui/LoadingState";
import { useState } from "react";
import { permissions } from "@/features/authorization/permissions";
import type { WorkItem } from "@/features/work-items/api/types";
import { ApiError } from "@/lib/api/apiClient";
import { useLabels, useSetWorkItemLabel } from "../hooks/useLabels";

export function WorkItemLabelsSection({ workItem, projectArchived }: { workItem: WorkItem; projectArchived: boolean }) {
  const labels = useLabels(workItem.projectId);
  const mutation = useSetWorkItemLabel(workItem.projectId, workItem.id);
  const [selected, setSelected] = useState("");
  const [error, setError] = useState<string | null>(null);
  const canAssign = !projectArchived && workItem.archivedAt === null && workItem.permissions.includes(permissions.labelAssign);
  const assignedIds = new Set(workItem.labels.map(label => label.id));
  const available = labels.data?.filter(label => !assignedIds.has(label.id)) ?? [];
  const setAssignment = async (labelId: string, assigned: boolean) => {
    setError(null);
    try { await mutation.mutateAsync({ labelId, assigned }); setSelected(""); }
    catch (cause) { setError(cause instanceof ApiError ? cause.message : "Label işlemi tamamlanamadı."); }
  };

  return <section aria-labelledby="work-item-labels-title" className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
    <h2 id="work-item-labels-title" className="text-lg font-semibold text-slate-900">WorkItem Labels</h2>
    {workItem.labels.length === 0 ? <p className="mt-3 text-sm text-slate-500">Atanmış Label yok.</p> : null}
    <ul className="mt-3 flex flex-wrap gap-2">{workItem.labels.map(label => <li key={label.id} className="flex items-center gap-2 rounded-md bg-slate-100 px-3 py-1 text-sm text-slate-700"><span className="break-all">{label.name}</span>{canAssign ? <button type="button" aria-label={`${label.name} Label kaldır`} disabled={mutation.isPending} onClick={() => void setAssignment(label.id, false)} className="font-semibold text-red-700 disabled:opacity-50">×</button> : null}</li>)}</ul>
    {canAssign ? <div className="mt-4 flex flex-wrap gap-2"><select aria-label="Atanacak Label" value={selected} onChange={event => setSelected(event.target.value)} disabled={labels.isLoading || labels.isError || mutation.isPending} className="min-w-48 rounded-md border border-slate-300 px-3 py-2 text-sm"><option value="">Label seçin</option>{available.map(label => <option key={label.id} value={label.id}>{label.name}</option>)}</select><button type="button" disabled={!available.some(label => label.id === selected) || mutation.isPending} onClick={() => void setAssignment(selected, true)} className="rounded-md bg-slate-900 px-4 py-2 text-sm text-white disabled:opacity-50">Label ata</button>{labels.isLoading ? <LoadingState label="Labels yükleniyor..." /> : null}{!labels.isLoading && !labels.isError && available.length === 0 ? <p className="self-center text-sm text-slate-500">Atanabilecek başka Label yok.</p> : null}</div> : null}
    {labels.isError && canAssign ? <p role="alert" className="mt-3 text-sm text-red-700">Label seçenekleri yüklenemedi.</p> : null}
    {error ? <p role="alert" className="mt-3 text-sm text-red-700">{error}</p> : null}
  </section>;
}
