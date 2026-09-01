import { useCurrentOrganization } from "../hooks/useCurrentOrganization";

export function OrganizationDashboardPage() {
  const organization = useCurrentOrganization();

  return (
    <section>
      <p className="text-sm font-medium text-slate-500">{organization.role}</p>
      <h1 className="mt-1 text-2xl font-semibold text-slate-900">{organization.name}</h1>
      <div className="mt-6 rounded-lg border border-dashed border-slate-300 bg-white p-8 text-sm text-slate-600">
        Organization tenant context’i hazır. Dashboard metrikleri FAZ 22’de eklenecek.
      </div>
    </section>
  );
}
