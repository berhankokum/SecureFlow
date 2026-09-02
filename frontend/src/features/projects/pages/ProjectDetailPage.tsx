import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { permissions } from "@/features/authorization/permissions";
import { useMembers } from "@/features/memberships/hooks/useMembers";
import { useCurrentOrganization } from "@/features/organizations/hooks/useCurrentOrganization";
import { ApiError } from "@/lib/api/apiClient";
import type { ProjectRole } from "../api/types";
import { ProjectForm } from "../components/ProjectForm";
import { useAddProjectMember, useArchiveProject, useChangeProjectMemberRole, useProject, useProjects, useRemoveProjectMember, useUpdateProject } from "../hooks/useProjects";
import type { UpdateProjectFormValues } from "../schemas/projectSchema";
import { ProjectWorkItemsSection } from "@/features/work-items/components/ProjectWorkItemsSection";

const roles: ProjectRole[] = ["MANAGER", "CONTRIBUTOR", "VIEWER"];

export function ProjectDetailPage() {
  const { projectKey = "" } = useParams();
  const organization = useCurrentOrganization();
  const projectsQuery = useProjects(organization.id);
  const summary = projectsQuery.data?.find(project => project.key === projectKey.toUpperCase());
  const projectId = summary?.id ?? "";
  const projectQuery = useProject(projectId);
  const membersQuery = useMembers(organization.id);
  const updateMutation = useUpdateProject(organization.id, projectId);
  const archiveMutation = useArchiveProject(organization.id, projectId);
  const addMutation = useAddProjectMember(organization.id, projectId);
  const changeRoleMutation = useChangeProjectMemberRole(organization.id, projectId);
  const removeMutation = useRemoveProjectMember(organization.id, projectId);
  const [editing, setEditing] = useState(false);
  const [selectedUserId, setSelectedUserId] = useState("");
  const [selectedRole, setSelectedRole] = useState<ProjectRole>("CONTRIBUTOR");
  const [actionError, setActionError] = useState<string | null>(null);

  if (projectsQuery.isLoading || (summary && projectQuery.isLoading)) return <p className="text-sm text-slate-600">Project yükleniyor...</p>;
  if (projectsQuery.isError || !summary || projectQuery.isError || !projectQuery.data) return <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">Project bulunamadı veya erişim yetkiniz yok.</p>;

  const project = projectQuery.data;
  const isArchived = project.archivedAt !== null;
  const canUpdate = project.permissions.includes(permissions.projectUpdate) && !isArchived;
  const canArchive = project.permissions.includes(permissions.projectArchive) && !isArchived;
  const memberIds = new Set(project.members.map(member => member.userId));
  const candidates = membersQuery.data?.filter(member => !memberIds.has(member.userId)) ?? [];

  const runAction = async (action: () => Promise<unknown>) => {
    setActionError(null);
    try { await action(); } catch (error) { setActionError(error instanceof ApiError ? error.message : "İşlem tamamlanamadı."); }
  };

  return <section>
    <Link to=".." relative="path" className="text-sm font-medium text-slate-600 hover:underline">← Projects</Link>
    <div className="mt-4 flex flex-wrap items-start justify-between gap-4">
      <div><div className="flex items-center gap-3"><span className="rounded bg-slate-900 px-2 py-1 text-xs font-semibold text-white">{project.key}</span><h1 className="text-2xl font-semibold text-slate-900">{project.name}</h1>{isArchived ? <span className="rounded-full bg-slate-200 px-2 py-1 text-xs text-slate-700">Archived</span> : null}</div><p className="mt-3 text-sm text-slate-600">{project.description || "Açıklama yok."}</p>{project.projectRole ? <p className="mt-2 text-xs text-slate-500">Project rolünüz: {project.projectRole}</p> : null}</div>
      <div className="flex gap-2">{canUpdate ? <button type="button" onClick={() => setEditing(true)} className="rounded-md border border-slate-300 px-3 py-2 text-sm">Ayarlar</button> : null}{canArchive ? <button type="button" disabled={archiveMutation.isPending} onClick={() => { if (window.confirm("Bu Project archive edilsin mi?")) void runAction(() => archiveMutation.mutateAsync()); }} className="rounded-md border border-red-300 px-3 py-2 text-sm text-red-700">Archive et</button> : null}</div>
    </div>
    {isArchived ? <p className="mt-5 rounded-md bg-amber-50 px-4 py-3 text-sm text-amber-800">Archived Project salt okunurdur; key ve membership geçmişi korunur.</p> : null}
    {actionError ? <p className="mt-5 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{actionError}</p> : null}
    {editing ? <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5"><p className="mb-4 text-xs text-slate-500">Project key immutable: {project.key}</p><ProjectForm mode="update" initialValues={{ name: project.name, description: project.description ?? "" }} onCancel={() => setEditing(false)} onSubmit={async values => { const updateValues = values as UpdateProjectFormValues; await updateMutation.mutateAsync({ name: updateValues.name, description: updateValues.description || undefined }); setEditing(false); }} /></div> : null}

    <ProjectWorkItemsSection project={project} />

    <section className="mt-8"><h2 className="text-lg font-semibold text-slate-900">Project üyeleri</h2>
      {canUpdate ? <div className="mt-3 flex max-w-3xl flex-wrap gap-2"><select aria-label="Eklenecek organization üyesi" value={selectedUserId} onChange={event => setSelectedUserId(event.target.value)} className="min-w-52 flex-1 rounded-md border border-slate-300 px-3 py-2 text-sm"><option value="">Üye seçin</option>{candidates.map(member => <option key={member.userId} value={member.userId}>{member.email}</option>)}</select><select aria-label="Yeni Project rolü" value={selectedRole} onChange={event => setSelectedRole(event.target.value as ProjectRole)} className="rounded-md border border-slate-300 px-3 py-2 text-sm">{roles.map(role => <option key={role}>{role}</option>)}</select><button type="button" disabled={!selectedUserId || addMutation.isPending} onClick={() => void runAction(async () => { await addMutation.mutateAsync({ userId: selectedUserId, role: selectedRole }); setSelectedUserId(""); })} className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">Ekle</button></div> : null}
      <div className="mt-4 rounded-lg border border-slate-200 bg-white">{project.members.length === 0 ? <p className="p-5 text-sm text-slate-500">Project üyesi yok.</p> : null}{project.members.map(member => <div key={member.userId} className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 px-5 py-4 last:border-b-0"><p className="text-sm font-medium text-slate-900">{member.email}</p>{canUpdate ? <div className="flex items-center gap-3"><select aria-label={`${member.email} Project rolü`} value={member.role} disabled={changeRoleMutation.isPending} onChange={event => void runAction(() => changeRoleMutation.mutateAsync({ userId: member.userId, role: event.target.value as ProjectRole }))} className="rounded-md border border-slate-300 px-2 py-1 text-sm">{roles.map(role => <option key={role}>{role}</option>)}</select><button type="button" disabled={removeMutation.isPending} onClick={() => void runAction(() => removeMutation.mutateAsync(member.userId))} className="text-sm font-medium text-red-700 hover:underline">Çıkar</button></div> : <span className="text-sm text-slate-600">{member.role}</span>}</div>)}</div>
    </section>
  </section>;
}
