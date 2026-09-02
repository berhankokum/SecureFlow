import { useState } from "react";
import { Link } from "react-router-dom";
import { permissions } from "@/features/authorization/permissions";
import type { ProjectDetails } from "@/features/projects/api/types";
import { toWorkItemPayload } from "../api/toWorkItemPayload";
import { WorkItemForm } from "./WorkItemForm";
import { useCreateWorkItem, useWorkItems } from "../hooks/useWorkItems";

export function ProjectWorkItemsSection({ project }: { project: ProjectDetails }) {
  const workItemsQuery = useWorkItems(project.id);
  const createMutation = useCreateWorkItem(project.id);
  const [creating, setCreating] = useState(false);
  const canCreate = project.archivedAt === null && project.permissions.includes(permissions.workItemCreate);

  return <section className="mt-8">
    <div className="flex items-center justify-between gap-4"><div><h2 className="text-lg font-semibold text-slate-900">WorkItems</h2><p className="mt-1 text-sm text-slate-600">{project.key} için çekirdek iş kayıtları.</p></div>{canCreate ? <button type="button" onClick={() => setCreating(true)} className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white">WorkItem oluştur</button> : null}</div>
    {creating ? <div className="mt-4 rounded-lg border border-slate-200 bg-white p-5"><WorkItemForm mode="create" onCancel={() => setCreating(false)} onSubmit={async values => { await createMutation.mutateAsync(toWorkItemPayload(values)); setCreating(false); }} /></div> : null}
    {workItemsQuery.isLoading ? <div className="mt-4 h-28 animate-pulse rounded-lg bg-slate-200" /> : null}
    {workItemsQuery.isError ? <p className="mt-4 rounded-md bg-red-50 p-3 text-sm text-red-700">WorkItems yüklenemedi.</p> : null}
    {workItemsQuery.data?.length === 0 ? <div className="mt-4 rounded-lg border border-dashed border-slate-300 p-7 text-center text-sm text-slate-600">Henüz WorkItem yok.</div> : null}
    <div className="mt-4 divide-y divide-slate-100 rounded-lg border border-slate-200 bg-white">
      {workItemsQuery.data?.map(workItem => <Link key={workItem.id} to={`../../work-items/${workItem.key}`} relative="path" className="flex flex-wrap items-center justify-between gap-4 px-5 py-4 hover:bg-slate-50"><div><div className="flex items-center gap-2"><span className="text-xs font-semibold text-slate-500">{workItem.key}</span><span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{workItem.type}</span>{workItem.archivedAt ? <span className="rounded bg-slate-200 px-2 py-0.5 text-xs text-slate-600">Archived</span> : null}</div><p className="mt-1 font-medium text-slate-900">{workItem.title}</p></div><div className="text-right"><p className="text-xs font-semibold text-slate-600">{workItem.priority}</p><p className="mt-1 text-xs text-slate-500">{workItem.dueDate ? `Due ${workItem.dueDate}` : "Due date yok"}</p></div></Link>)}
    </div>
  </section>;
}
