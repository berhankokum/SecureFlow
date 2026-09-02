import { useState } from "react";
import { Link } from "react-router-dom";
import { usePermission } from "@/features/authorization/hooks/usePermission";
import { permissions } from "@/features/authorization/permissions";
import { useCurrentOrganization } from "@/features/organizations/hooks/useCurrentOrganization";
import { TeamForm } from "../components/TeamForm";
import { useCreateTeam, useTeams } from "../hooks/useTeams";

export function TeamsPage() {
  const organization = useCurrentOrganization();
  const canCreate = usePermission(permissions.teamCreate);
  const teamsQuery = useTeams(organization.id);
  const createMutation = useCreateTeam(organization.id);
  const [creating, setCreating] = useState(false);

  return (
    <section>
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">Teams</h1>
          <p className="mt-1 text-sm text-slate-600">{organization.name} içindeki ekipleri yönetin.</p>
        </div>
        {canCreate ? (
          <button type="button" onClick={() => setCreating(true)} className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white">Team oluştur</button>
        ) : null}
      </div>

      {creating ? (
        <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
          <TeamForm
            title="Yeni Team"
            submitLabel="Team oluştur"
            onCancel={() => setCreating(false)}
            onSubmit={async (values) => {
              await createMutation.mutateAsync({ name: values.name, description: values.description || undefined });
              setCreating(false);
            }}
          />
        </div>
      ) : null}

      {teamsQuery.isLoading ? <p className="mt-6 text-sm text-slate-600">Teams yükleniyor...</p> : null}
      {teamsQuery.isError ? <p className="mt-6 rounded-md bg-red-50 p-3 text-sm text-red-700">Teams yüklenemedi.</p> : null}
      {teamsQuery.data?.length === 0 ? <p className="mt-6 rounded-lg border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500">Henüz Team yok.</p> : null}
      <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {teamsQuery.data?.map((team) => (
          <Link key={team.id} to={team.id} className="rounded-lg border border-slate-200 bg-white p-5 hover:border-slate-400">
            <div className="flex items-start justify-between gap-3">
              <h2 className="font-semibold text-slate-900">{team.name}</h2>
              {team.archivedAt ? <span className="rounded-full bg-slate-100 px-2 py-1 text-xs text-slate-600">Archived</span> : null}
            </div>
            <p className="mt-2 line-clamp-2 text-sm text-slate-600">{team.description || "Açıklama yok."}</p>
            <p className="mt-4 text-xs text-slate-500">{team.memberCount} üye</p>
          </Link>
        ))}
      </div>
    </section>
  );
}
