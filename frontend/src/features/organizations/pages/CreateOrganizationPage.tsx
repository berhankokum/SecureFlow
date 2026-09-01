import { Link } from "react-router-dom";
import { CreateOrganizationForm } from "../components/CreateOrganizationForm";

export function CreateOrganizationPage() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-10">
      <section className="w-full max-w-lg rounded-xl border border-slate-200 bg-white p-8 shadow-sm">
        <p className="text-sm font-medium text-slate-500">SecureFlow</p>
        <h1 className="mt-2 text-2xl font-semibold text-slate-900">Organization oluştur</h1>
        <p className="mt-2 text-sm text-slate-600">
          Şirketiniz veya çalışma alanınız için güvenli bir tenant oluşturun. Otomatik olarak OWNER olacaksınız.
        </p>
        <div className="mt-6">
          <CreateOrganizationForm />
        </div>
        <Link to="/" className="mt-5 block text-center text-sm text-slate-600 underline">
          Organization listesine dön
        </Link>
      </section>
    </main>
  );
}
