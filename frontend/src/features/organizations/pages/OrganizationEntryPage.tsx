import { Link, Navigate } from "react-router-dom";
import { useOrganizations } from "../hooks/useOrganizations";

export function OrganizationEntryPage() {
  const { data: organizations, isLoading, isError, refetch } = useOrganizations();

  if (isLoading) {
    return <div className="p-6 text-sm text-slate-600">Organization’lar yükleniyor...</div>;
  }

  if (isError) {
    return (
      <div className="p-6 text-sm text-red-700">
        <p>Organization listesi yüklenemedi.</p>
        <button type="button" className="mt-3 underline" onClick={() => void refetch()}>
          Tekrar dene
        </button>
      </div>
    );
  }

  if (organizations && organizations.length > 0) {
    return <Navigate to={`/app/${organizations[0].slug}/dashboard`} replace />;
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <section className="max-w-lg rounded-xl border border-slate-200 bg-white p-8 text-center shadow-sm">
        <h1 className="text-2xl font-semibold text-slate-900">İlk organization’ınızı oluşturun</h1>
        <p className="mt-3 text-sm text-slate-600">
          Proje, ekip ve iş akışları bu güvenli tenant altında tutulacak.
        </p>
        <Link
          to="/organizations/new"
          className="mt-6 inline-flex rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white"
        >
          Organization oluştur
        </Link>
      </section>
    </main>
  );
}
