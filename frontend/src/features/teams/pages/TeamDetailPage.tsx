import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { usePermission } from "@/features/authorization/hooks/usePermission";
import { permissions } from "@/features/authorization/permissions";
import { useMembers } from "@/features/memberships/hooks/useMembers";
import { useCurrentOrganization } from "@/features/organizations/hooks/useCurrentOrganization";
import { ApiError } from "@/lib/api/apiClient";
import { TeamForm } from "../components/TeamForm";
import { useAddTeamMember, useArchiveTeam, useRemoveTeamMember, useTeam, useUpdateTeam } from "../hooks/useTeams";

export function TeamDetailPage() {
  const { teamId = "" } = useParams();
  const organization = useCurrentOrganization();
  const teamQuery = useTeam(teamId);
  const membersQuery = useMembers(organization.id);
  const canUpdate = usePermission(permissions.teamUpdate);
  const canArchive = usePermission(permissions.teamArchive);
  const canManageMembers = usePermission(permissions.teamMemberManage);
  const updateMutation = useUpdateTeam(organization.id, teamId);
  const archiveMutation = useArchiveTeam(organization.id, teamId);
  const addMutation = useAddTeamMember(teamId);
  const removeMutation = useRemoveTeamMember(teamId);
  const [editing, setEditing] = useState(false);
  const [selectedUserId, setSelectedUserId] = useState("");
  const [actionError, setActionError] = useState<string | null>(null);

  if (teamQuery.isLoading) return <p className="text-sm text-slate-600">Team yükleniyor...</p>;
  if (teamQuery.isError || !teamQuery.data) return <p className="rounded-md bg-red-50 p-3 text-sm text-red-700">Team yüklenemedi.</p>;

  const team = teamQuery.data;
  const isArchived = team.archivedAt !== null;
  const memberIds = new Set(team.members.map((member) => member.userId));
  const candidates = membersQuery.data?.filter((member) => !memberIds.has(member.userId)) ?? [];

  const runAction = async (action: () => Promise<unknown>) => {
    setActionError(null);
    try { await action(); } catch (error) {
      setActionError(error instanceof ApiError ? error.message : "İşlem tamamlanamadı.");
    }
  };

  return (
    <section>
      <Link to=".." relative="path" className="text-sm font-medium text-slate-600 hover:underline">← Teams</Link>
      <div className="mt-4 flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-semibold text-slate-900">{team.name}</h1>
            {isArchived ? <span className="rounded-full bg-slate-200 px-2 py-1 text-xs text-slate-700">Archived</span> : null}
          </div>
          <p className="mt-2 text-sm text-slate-600">{team.description || "Açıklama yok."}</p>
        </div>
        {!isArchived ? (
          <div className="flex gap-2">
            {canUpdate ? <button type="button" onClick={() => setEditing(true)} className="rounded-md border border-slate-300 px-3 py-2 text-sm">Düzenle</button> : null}
            {canArchive ? <button type="button" disabled={archiveMutation.isPending} onClick={() => { if (window.confirm("Bu Team archive edilsin mi?")) void runAction(() => archiveMutation.mutateAsync()); }} className="rounded-md border border-red-300 px-3 py-2 text-sm text-red-700">Archive et</button> : null}
          </div>
        ) : null}
      </div>

      {isArchived ? <p className="mt-5 rounded-md bg-amber-50 px-4 py-3 text-sm text-amber-800">Archived Team salt okunurdur; üyelikler geçmiş kaydı olarak korunur.</p> : null}
      {actionError ? <p className="mt-5 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{actionError}</p> : null}

      {editing ? (
        <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
          <TeamForm
            title="Team'i düzenle"
            submitLabel="Kaydet"
            initialValues={{ name: team.name, description: team.description ?? "" }}
            onCancel={() => setEditing(false)}
            onSubmit={async (values) => {
              await updateMutation.mutateAsync({ name: values.name, description: values.description || undefined });
              setEditing(false);
            }}
          />
        </div>
      ) : null}

      <section className="mt-8">
        <h2 className="text-lg font-semibold text-slate-900">Team üyeleri</h2>
        {canManageMembers && !isArchived ? (
          <div className="mt-3 flex max-w-xl gap-2">
            <select aria-label="Eklenecek organization üyesi" value={selectedUserId} onChange={(event) => setSelectedUserId(event.target.value)} className="min-w-0 flex-1 rounded-md border border-slate-300 px-3 py-2 text-sm">
              <option value="">Üye seçin</option>
              {candidates.map((member) => <option key={member.userId} value={member.userId}>{member.email}</option>)}
            </select>
            <button type="button" disabled={!selectedUserId || addMutation.isPending} onClick={() => void runAction(async () => { await addMutation.mutateAsync(selectedUserId); setSelectedUserId(""); })} className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">Ekle</button>
          </div>
        ) : null}

        <div className="mt-4 rounded-lg border border-slate-200 bg-white">
          {team.members.length === 0 ? <p className="p-5 text-sm text-slate-500">Bu Team'de henüz üye yok.</p> : null}
          {team.members.map((member) => (
            <div key={member.userId} className="flex items-center justify-between border-b border-slate-100 px-5 py-4 last:border-b-0">
              <div><p className="text-sm font-medium text-slate-900">{member.email}</p><p className="mt-1 text-xs text-slate-500">Eklenme: {new Intl.DateTimeFormat("tr-TR", { dateStyle: "medium" }).format(new Date(member.addedAt))}</p></div>
              {canManageMembers && !isArchived ? <button type="button" disabled={removeMutation.isPending} onClick={() => void runAction(() => removeMutation.mutateAsync(member.userId))} className="text-sm font-medium text-red-700 hover:underline">Çıkar</button> : null}
            </div>
          ))}
        </div>
      </section>
    </section>
  );
}
