import { useState } from "react";
import { LoadingState } from "@/components/ui/LoadingState";
import { ErrorState } from "@/components/ui/ErrorState";
import { Link } from "react-router-dom";
import { usePermission } from "@/features/authorization/hooks/usePermission";
import { permissions } from "@/features/authorization/permissions";
import { useCurrentOrganization } from "@/features/organizations/hooks/useCurrentOrganization";
import { ProjectForm } from "../components/ProjectForm";
import { useCreateProject, useProjects } from "../hooks/useProjects";
import type { CreateProjectFormValues } from "../schemas/projectSchema";

export function ProjectsPage() {
  const organization = useCurrentOrganization();
  const canCreate = usePermission(permissions.projectCreate) && !organization.archivedAt;
  const projectsQuery = useProjects(organization.id);
  const createMutation = useCreateProject(organization.id);
  const [creating, setCreating] = useState(false);

  return <section>
    <div className="flex flex-wrap items-center justify-between gap-4">
      <div><h1 className="text-2xl font-semibold text-slate-900">Projects</h1><p className="mt-1 text-sm text-slate-600">{organization.name} içindeki projeleri görüntüleyin.</p></div>
      {canCreate ? <button type="button" onClick={() => setCreating(true)} className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white">Project oluştur</button> : null}
    </div>
    {creating ? <div className="mt-6 rounded-lg border border-slate-200 bg-white p-5">
      <ProjectForm mode="create" onCancel={() => setCreating(false)} onSubmit={async values => {
        const createValues = values as CreateProjectFormValues;
        await createMutation.mutateAsync({ key: createValues.key.toUpperCase(), name: createValues.name, description: createValues.description || undefined });
        setCreating(false);
      }} />
    </div> : null}
    {projectsQuery.isLoading ? <LoadingState label="Projects yükleniyor..." /> : null}
    {projectsQuery.isError ? <ErrorState message="Projects yüklenemedi." onRetry={() => void projectsQuery.refetch()} retrying={projectsQuery.isFetching} /> : null}
    {projectsQuery.data?.length === 0 ? <div className="mt-6 rounded-lg border border-dashed border-slate-300 p-8 text-center"><p className="text-sm text-slate-600">Henüz erişebildiğiniz bir Project yok.</p>{canCreate ? <button type="button" onClick={() => setCreating(true)} className="mt-3 text-sm font-medium text-slate-900 underline">İlk Project'i oluştur</button> : null}</div> : null}
    <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      {projectsQuery.data?.map(project => <Link key={project.id} to={project.key} className="rounded-lg border border-slate-200 bg-white p-5 hover:border-slate-400">
        <div className="flex items-start justify-between gap-3"><div><span className="text-xs font-semibold text-slate-500">{project.key}</span><h2 className="mt-1 font-semibold text-slate-900">{project.name}</h2></div>{project.archivedAt ? <span className="rounded-full bg-slate-100 px-2 py-1 text-xs text-slate-600">Archived</span> : null}</div>
        <p className="mt-2 line-clamp-2 text-sm text-slate-600">{project.description || "Açıklama yok."}</p>
        {project.projectRole ? <p className="mt-4 text-xs text-slate-500">Rol: {project.projectRole}</p> : null}
      </Link>)}
    </div>
  </section>;
}
