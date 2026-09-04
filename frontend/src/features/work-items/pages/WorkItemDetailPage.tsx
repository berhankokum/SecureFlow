import { useState } from "react";
import { WorkItemLabelsSection } from "@/features/labels/components/WorkItemLabelsSection";
import { Link, useParams } from "react-router-dom";
import { permissions } from "@/features/authorization/permissions";
import { useCurrentOrganization } from "@/features/organizations/hooks/useCurrentOrganization";
import { useProject, useProjects } from "@/features/projects/hooks/useProjects";
import { ApiError } from "@/lib/api/apiClient";
import { toWorkItemPayload } from "../api/toWorkItemPayload";
import { WorkItemForm } from "../components/WorkItemForm";
import { WorkItemAssignmentSection } from "../components/WorkItemAssignmentSection";
import { useArchiveWorkItem, useUpdateWorkItem, useWorkItem, useWorkItems } from "../hooks/useWorkItems";

export function WorkItemDetailPage() {
  const { workItemKey = "" } = useParams();
  const organization = useCurrentOrganization();
  const projectKey = workItemKey.slice(0, workItemKey.lastIndexOf("-")).toUpperCase();
  const projectsQuery = useProjects(organization.id);
  const project = projectsQuery.data?.find(item => item.key === projectKey);
  const projectQuery = useProject(project?.id ?? "");
  const workItemsQuery = useWorkItems(project?.id ?? "");
  const summary = workItemsQuery.data?.find(item => item.key === workItemKey.toUpperCase());
  const workItemQuery = useWorkItem(summary?.id ?? "");
  const workItem = workItemQuery.data;
  const updateMutation = useUpdateWorkItem(project?.id ?? "", summary?.id ?? "");
  const archiveMutation = useArchiveWorkItem(project?.id ?? "", summary?.id ?? "");
  const [editing, setEditing] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  if (projectsQuery.isLoading || (project && (projectQuery.isLoading || workItemsQuery.isLoading)) || (summary && workItemQuery.isLoading)) return <p className="text-sm text-slate-600">WorkItem yükleniyor...</p>;
  if (projectsQuery.isError || !project || projectQuery.isError || !projectQuery.data || workItemsQuery.isError || !summary || workItemQuery.isError || !workItem) return <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">WorkItem bulunamadı veya erişim yetkiniz yok.</p>;

  const projectArchived = project.archivedAt !== null;
  const isArchived = workItem.archivedAt !== null;
  const canUpdate = !projectArchived && !isArchived && workItem.permissions.includes(permissions.workItemUpdate);
  const canArchive = !projectArchived && !isArchived && workItem.permissions.includes(permissions.workItemArchive);

  const runAction = async (action: () => Promise<unknown>) => {
    setActionError(null);
    try { await action(); } catch (error) { setActionError(error instanceof ApiError ? error.message : "İşlem tamamlanamadı."); }
  };

  return <section>
    <Link to={`../../projects/${project.key}`} relative="path" className="text-sm font-medium text-slate-600 hover:underline">← {project.key} Project</Link>
    <div className="mt-4 flex flex-wrap items-start justify-between gap-4"><div><div className="flex flex-wrap items-center gap-2"><span className="rounded bg-slate-900 px-2 py-1 text-xs font-semibold text-white">{workItem.key}</span><span className="rounded bg-slate-100 px-2 py-1 text-xs text-slate-700">{workItem.type}</span><span className="rounded bg-amber-50 px-2 py-1 text-xs font-semibold text-amber-800">{workItem.priority}</span>{isArchived ? <span className="rounded bg-slate-200 px-2 py-1 text-xs text-slate-700">Archived</span> : null}</div><h1 className="mt-3 text-2xl font-semibold text-slate-900">{workItem.title}</h1><p className="mt-3 whitespace-pre-wrap text-sm text-slate-600">{workItem.description || "Açıklama yok."}</p></div><div className="flex gap-2">{canUpdate ? <button type="button" onClick={() => setEditing(true)} className="rounded-md border border-slate-300 px-3 py-2 text-sm">Düzenle</button> : null}{canArchive ? <button type="button" disabled={archiveMutation.isPending} onClick={() => { if (window.confirm("Bu WorkItem archive edilsin mi?")) void runAction(() => archiveMutation.mutateAsync()); }} className="rounded-md border border-red-300 px-3 py-2 text-sm text-red-700">Archive et</button> : null}</div></div>
    <dl className="mt-6 grid gap-4 rounded-lg border border-slate-200 bg-white p-5 text-sm sm:grid-cols-4"><div><dt className="text-slate-500">State</dt><dd className="mt-1 font-medium text-slate-900">{workItem.workflowStateName.replaceAll("_", " ")}</dd></div><div><dt className="text-slate-500">Reporter</dt><dd className="mt-1 font-medium text-slate-900">{workItem.reporterEmail}</dd></div><div><dt className="text-slate-500">Due date</dt><dd className="mt-1 font-medium text-slate-900">{workItem.dueDate ?? "Yok"}</dd></div><div><dt className="text-slate-500">Son güncelleme</dt><dd className="mt-1 font-medium text-slate-900">{new Date(workItem.updatedAt).toLocaleString("tr-TR")}</dd></div></dl>
    {projectArchived ? <p className="mt-5 rounded-md bg-amber-50 px-4 py-3 text-sm text-amber-800">Project archived olduğu için WorkItem salt okunurdur.</p> : isArchived ? <p className="mt-5 rounded-md bg-amber-50 px-4 py-3 text-sm text-amber-800">Archived WorkItem salt okunurdur.</p> : null}
    <WorkItemAssignmentSection workItem={workItem} project={projectQuery.data} />
    <WorkItemLabelsSection workItem={workItem} projectArchived={projectArchived} />
    {actionError ? <p className="mt-5 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{actionError}</p> : null}
    {editing ? <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5"><WorkItemForm mode="update" initialValues={{ type: workItem.type, title: workItem.title, description: workItem.description ?? "", priority: workItem.priority, dueDate: workItem.dueDate ?? "" }} onCancel={() => setEditing(false)} onSubmit={async values => { await updateMutation.mutateAsync(toWorkItemPayload(values)); setEditing(false); }} /></div> : null}
  </section>;
}
