import { Outlet, useParams } from "react-router-dom";
import { LoadingState } from "@/components/ui/LoadingState";
import { OrganizationProvider } from "@/features/organizations/context/OrganizationProvider";
import { useOrganizations } from "@/features/organizations/hooks/useOrganizations";
import { RealtimeProvider } from "@/features/realtime/RealtimeProvider";

export function OrganizationRoute() {
  const { organizationSlug } = useParams();
  const { data: organizations, isLoading, isError, refetch } = useOrganizations();

  if (isLoading) {
    return <LoadingState label="Tenant erişimi doğrulanıyor..." />;
  }

  if (isError) {
    return (
      <div className="p-6 text-sm text-red-700">
        <p role="alert">Tenant erişimi doğrulanamadı.</p>
        <button type="button" className="mt-3 underline" onClick={() => void refetch()}>
          Tekrar dene
        </button>
      </div>
    );
  }

  const organization = organizations?.find((item) => item.slug === organizationSlug);
  if (!organization) {
    return (
      <div role="alert" className="p-6 text-sm text-red-700">
        Bu organization bulunamadı veya erişim yetkiniz yok.
      </div>
    );
  }

  return (
    <OrganizationProvider organization={organization}>
      <RealtimeProvider key={organization.id} organizationId={organization.id}>
        <Outlet />
      </RealtimeProvider>
    </OrganizationProvider>
  );
}
