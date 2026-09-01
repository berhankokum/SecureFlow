import { useCurrentOrganization } from "../hooks/useCurrentOrganization";

export function OrganizationSettingsPage() {
  const organization = useCurrentOrganization();

  return (
    <section className="max-w-2xl">
      <h1 className="text-2xl font-semibold text-slate-900">Organization ayarları</h1>
      <dl className="mt-6 divide-y divide-slate-200 rounded-lg border border-slate-200 bg-white">
        <div className="grid grid-cols-3 gap-4 px-5 py-4 text-sm">
          <dt className="font-medium text-slate-600">Ad</dt>
          <dd className="col-span-2 text-slate-900">{organization.name}</dd>
        </div>
        <div className="grid grid-cols-3 gap-4 px-5 py-4 text-sm">
          <dt className="font-medium text-slate-600">Slug</dt>
          <dd className="col-span-2 font-mono text-slate-900">{organization.slug}</dd>
        </div>
        <div className="grid grid-cols-3 gap-4 px-5 py-4 text-sm">
          <dt className="font-medium text-slate-600">Rolünüz</dt>
          <dd className="col-span-2 text-slate-900">{organization.role}</dd>
        </div>
      </dl>
      <p className="mt-4 text-sm text-slate-500">
        Bu route <code>organization.manage</code> izniyle korunur. Frontend kontrolü UX,
        backend resource authorization ise güvenlik sınırıdır.
      </p>
    </section>
  );
}
