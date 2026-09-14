import { useMemo, useState } from "react";
import { useCurrentUser } from "@/features/auth/hooks/useCurrentUser";
import { permissions } from "@/features/authorization/permissions";
import { useMembers } from "@/features/memberships/hooks/useMembers";
import { useCurrentOrganization } from "@/features/organizations/hooks/useCurrentOrganization";
import type { ProjectDetails } from "@/features/projects/api/types";
import { ApiError } from "@/lib/api/apiClient";
import type { WorkItem } from "../api/types";
import { useAssignWorkItem, useSelfAssignWorkItem, useUnassignWorkItem } from "../hooks/useWorkItems";

export function WorkItemAssignmentSection({ workItem, project }: { workItem: WorkItem; project: ProjectDetails }) {
  const currentUser = useCurrentUser();
  const organization = useCurrentOrganization();
  const hasOrganizationWideAccess = organization.role === "OWNER" || organization.role === "ADMIN";
  const organizationMembers = useMembers(organization.id, hasOrganizationWideAccess);
  const assign = useAssignWorkItem(project.id, workItem.id, workItem.version);
  const selfAssign = useSelfAssignWorkItem(project.id, workItem.id, workItem.version);
  const unassign = useUnassignWorkItem(project.id, workItem.id, workItem.version);
  const [selectedUserId, setSelectedUserId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const readOnly = project.archivedAt !== null || workItem.archivedAt !== null;
  const canAssignOther = !readOnly && workItem.permissions.includes(permissions.workItemAssign);
  const canSelfAssign = !readOnly && workItem.permissions.includes(permissions.workItemSelfAssign)
    && currentUser.data?.id !== workItem.assigneeId;
  const canUnassign = !readOnly && workItem.assigneeId !== null && (
    workItem.permissions.includes(permissions.workItemAssign)
    || (workItem.assigneeId === currentUser.data?.id
      && workItem.permissions.includes(permissions.workItemSelfAssign)));
  const candidates = useMemo(() => {
    const byId = new Map(project.members.map(member => [member.userId, { id: member.userId, email: member.email }]));
    if (hasOrganizationWideAccess) {
      for (const member of organizationMembers.data ?? []) {
        if (member.role === "OWNER" || member.role === "ADMIN") {
          byId.set(member.userId, { id: member.userId, email: member.email });
        }
      }
    }
    return [...byId.values()].sort((left, right) => left.email.localeCompare(right.email));
  }, [hasOrganizationWideAccess, organizationMembers.data, project.members]);
  const pending = assign.isPending || selfAssign.isPending || unassign.isPending;
  const run = async (action: () => Promise<unknown>) => {
    setError(null);
    try { await action(); setSelectedUserId(""); }
    catch (cause) { setError(cause instanceof ApiError ? cause.message : "Assignment işlemi tamamlanamadı."); }
  };

  return <section aria-labelledby="assignment-title" className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
    <h2 id="assignment-title" className="text-lg font-semibold text-slate-900">Assignment</h2>
    <p className="mt-2 text-sm text-slate-600">Assignee: <span className="font-medium text-slate-900">{workItem.assigneeEmail ?? "Unassigned"}</span></p>
    {(canAssignOther || canSelfAssign || canUnassign) ? <div className="mt-4 flex flex-wrap items-center gap-2">
      {canAssignOther ? <><select aria-label="Assignee" value={selectedUserId} onChange={event => setSelectedUserId(event.target.value)} disabled={pending || organizationMembers.isLoading} className="min-w-56 rounded-md border border-slate-300 px-3 py-2 text-sm"><option value="">Kullanıcı seçin</option>{candidates.map(candidate => <option key={candidate.id} value={candidate.id}>{candidate.email}</option>)}</select><button type="button" disabled={pending || !candidates.some(candidate => candidate.id === selectedUserId)} onClick={() => void run(() => assign.mutateAsync(selectedUserId))} className="rounded-md bg-slate-900 px-4 py-2 text-sm text-white disabled:opacity-50">{workItem.assigneeId ? "Yeniden ata" : "Ata"}</button></> : null}
      {canSelfAssign ? <button type="button" disabled={pending} onClick={() => void run(() => selfAssign.mutateAsync())} className="rounded-md border border-slate-300 px-4 py-2 text-sm text-slate-700 disabled:opacity-50">Kendime ata</button> : null}
      {canUnassign ? <button type="button" disabled={pending} onClick={() => void run(() => unassign.mutateAsync())} className="rounded-md border border-red-300 px-4 py-2 text-sm text-red-700 disabled:opacity-50">Atamayı kaldır</button> : null}
    </div> : null}
    {error ? <p role="alert" className="mt-3 text-sm text-red-700">{error}</p> : null}
  </section>;
}
