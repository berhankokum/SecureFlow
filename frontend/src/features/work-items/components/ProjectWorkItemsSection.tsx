import { useState } from "react";
import { Link } from "react-router-dom";
import { permissions } from "@/features/authorization/permissions";
import { useCurrentUser } from "@/features/auth/hooks/useCurrentUser";
import type { ProjectDetails } from "@/features/projects/api/types";
import { toWorkItemPayload } from "../api/toWorkItemPayload";
import { WorkItemForm } from "./WorkItemForm";
import { useCreateWorkItem, useWorkItems } from "../hooks/useWorkItems";

export function ProjectWorkItemsSection({ project }: { project: ProjectDetails }) {
  const workItemsQuery = useWorkItems(project.id);
  const createMutation = useCreateWorkItem(project.id);
  const currentUser = useCurrentUser();
  const [creating, setCreating] = useState(false);
  const [myWorkOnly, setMyWorkOnly] = useState(false);
  const canCreate = project.archivedAt === null && project.permissions.includes(permissions.workItemCreate);

  const visibleWorkItems = myWorkOnly
    ? workItemsQuery.data?.filter(workItem => workItem.assigneeId === currentUser.data?.id)
    : workItemsQuery.data;

  return <section className="mt-8">
    <div className="flex items-center justify-between gap-4"><div><h2 className="text-lg font-semibold text-slate-900">WorkItems</h2><p className="mt-1 text-sm text-slate-600">{project.key} için çekirdek iş kayıtları.</p></div>{canCreate ? <button type="button" onClick={() => setCreating(true)} className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white">WorkItem oluştur</button> : null}</div>
    {creating ? <div className="mt-4 rounded-lg border border-slate-200 bg-white p-5"><WorkItemForm mode="create" onCancel={() => setCreating(false)} onSubmit={async values => { await createMutation.mutateAsync(toWorkItemPayload(values)); setCreating(false); }} /></div> : null}
    {workItemsQuery.isLoading ? <div className="mt-4 h-28 animate-pulse rounded-lg bg-slate-200" /> : null}
    {workItemsQuery.isError ? <p className="mt-4 rounded-md bg-red-50 p-3 text-sm text-red-700">WorkItems yüklenemedi.</p> : null}
    <div className="mt-4 flex gap-2" role="group" aria-label="WorkItem görünümü"><button type="button" aria-pressed={!myWorkOnly} onClick={() => setMyWorkOnly(false)} className={`rounded-md px-3 py-2 text-sm ${!myWorkOnly ? "bg-slate-900 text-white" : "border border-slate-300 text-slate-700"}`}>Tüm WorkItems</button><button type="button" aria-pressed={myWorkOnly} onClick={() => setMyWorkOnly(true)} className={`rounded-md px-3 py-2 text-sm ${myWorkOnly ? "bg-slate-900 text-white" : "border border-slate-300 text-slate-700"}`}>Bana atananlar</button></div>
    {visibleWorkItems?.length === 0 ? <div className="mt-4 rounded-lg border border-dashed border-slate-300 p-7 text-center text-sm text-slate-600">{myWorkOnly ? "Size atanmış WorkItem yok." : "Henüz WorkItem yok."}</div> : null}
    <div className="mt-4 divide-y divide-slate-100 rounded-lg border border-slate-200 bg-white">
      {visibleWorkItems?.map(workItem => <Link key={workItem.id} to={`../../work-items/${workItem.key}`} relative="path" className="flex flex-wrap items-center justify-between gap-4 px-5 py-4 hover:bg-slate-50">
        <div><div className="flex items-center gap-2"><span className="text-xs font-semibold text-slate-500">{workItem.key}</span><span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{workItem.type}</span>{workItem.archivedAt ? <span className="rounded bg-slate-200 px-2 py-0.5 text-xs text-slate-600">Archived</span> : null}</div><p className="mt-1 font-medium text-slate-900">{workItem.title}</p>
          <div className="mt-2 flex flex-wrap gap-1">{workItem.labels.map(label => <span key={label.id} className="break-all rounded bg-blue-50 px-2 py-0.5 text-xs text-blue-800">{label.name}</span>)}</div>
        </div>
        <div className="text-right"><p className="text-xs font-semibold text-slate-600">{workItem.priority}</p><p className="mt-1 text-xs text-slate-500">{workItem.assigneeEmail ?? "Unassigned"}</p><p className="mt-1 text-xs text-slate-500">{workItem.dueDate ? `Due ${workItem.dueDate}` : "Due date yok"}</p></div>
      </Link>)}
    </div>
  </section>;
}
