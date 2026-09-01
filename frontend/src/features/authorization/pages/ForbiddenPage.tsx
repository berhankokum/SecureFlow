import { Link } from "react-router-dom";
import { useCurrentOrganization } from "@/features/organizations/hooks/useCurrentOrganization";

export function ForbiddenPage() {
  const organization = useCurrentOrganization();

  return (
    <section className="mx-auto max-w-xl rounded-xl border border-amber-200 bg-amber-50 p-8 text-center">
      <p className="text-sm font-semibold uppercase tracking-wide text-amber-700">403 · Yetkisiz işlem</p>
      <h1 className="mt-2 text-2xl font-semibold text-slate-900">Bu alanı görüntüleme yetkiniz yok</h1>
      <p className="mt-3 text-sm text-slate-600">
        Gerekli izne sahip olduğunuzu düşünüyorsanız organization yöneticinizle iletişime geçin.
      </p>
      <Link
        to={`/app/${organization.slug}/dashboard`}
        className="mt-6 inline-block rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white"
      >
        Dashboard'a dön
      </Link>
    </section>
  );
}
