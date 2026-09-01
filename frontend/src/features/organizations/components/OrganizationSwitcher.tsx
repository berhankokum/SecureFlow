import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { useCurrentOrganization } from "../hooks/useCurrentOrganization";
import { useOrganizations } from "../hooks/useOrganizations";

export function OrganizationSwitcher() {
  const currentOrganization = useCurrentOrganization();
  const { data: organizations = [] } = useOrganizations();
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const switchOrganization = (slug: string) => {
    if (slug === currentOrganization.slug) {
      return;
    }

    queryClient.removeQueries({
      predicate: (query) =>
        query.queryKey[0] === "tenant"
        || query.queryKey[0] === "memberships"
        || query.queryKey[0] === "invitations",
    });
    navigate(`/app/${slug}/dashboard`);
  };

  return (
    <div className="space-y-2">
      <label htmlFor="organization-switcher" className="text-xs font-semibold uppercase tracking-wide text-slate-500">
        Organization
      </label>
      <select
        id="organization-switcher"
        value={currentOrganization.slug}
        onChange={(event) => switchOrganization(event.target.value)}
        className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm"
      >
        {organizations.map((organization) => (
          <option key={organization.id} value={organization.slug}>
            {organization.name}
          </option>
        ))}
      </select>
      <Link to="/organizations/new" className="block text-xs text-slate-600 underline">
        Yeni organization oluştur
      </Link>
    </div>
  );
}
