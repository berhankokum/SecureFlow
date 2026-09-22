import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useCurrentUser } from "@/features/auth/hooks/useCurrentUser";
import { useLogout } from "@/features/auth/hooks/useLogout";
import { OrganizationSwitcher } from "@/features/organizations/components/OrganizationSwitcher";
import { useCurrentOrganization } from "@/features/organizations/hooks/useCurrentOrganization";
import { usePermission } from "@/features/authorization/hooks/usePermission";
import { permissions } from "@/features/authorization/permissions";
import { NotificationBell } from "@/features/notifications/components/NotificationBell";

/** FAZ 3 tenant-aware application shell. */
export function AppShell() {
  const { data: user } = useCurrentUser();
  const organization = useCurrentOrganization();
  const canManageOrganization = usePermission(permissions.organizationManage);
  const canViewAudit = usePermission(permissions.auditView);
  const logoutMutation = useLogout();
  const navigate = useNavigate();

  const handleLogout = async () => {
    try {
      await logoutMutation.mutateAsync();
      navigate("/login", { replace: true });
    } catch { /* Mutation state renders a retryable error below. */ }
  };

  return (
    <div className="flex min-h-screen flex-col bg-slate-50 text-slate-900">
      <a href="#main-content" className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded focus:bg-white focus:p-3">Ana içeriğe geç</a>
      <header className="flex flex-col items-start gap-3 border-b border-slate-200 bg-white px-6 py-4 sm:flex-row sm:items-center sm:justify-between">
        <span className="text-lg font-semibold tracking-tight">SecureFlow</span>
        {user ? (
          <div className="flex min-w-0 max-w-full flex-wrap items-center gap-4 text-sm text-slate-600">
            <span className="min-w-0 break-all">{user.email}</span>
            <NotificationBell />
            <button
              type="button"
              onClick={handleLogout}
              disabled={logoutMutation.isPending}
              className="shrink-0 rounded-md border border-slate-300 px-3 py-1 text-sm hover:bg-slate-50 disabled:opacity-50"
            >
              Çıkış yap
            </button>
          </div>
        ) : null}
      </header>
      {logoutMutation.isError ? <p role="alert" className="bg-red-50 p-3 text-sm text-red-700">Çıkış yapılamadı. Yeniden deneyin.</p> : null}
      <div className="flex flex-1 flex-col md:flex-row">
        <aside className="w-full border-b border-slate-200 bg-white p-4 md:w-64 md:border-b-0 md:border-r">
          <OrganizationSwitcher />
          <nav aria-label="Ana menü" className="mt-6 space-y-1 text-sm">
            <NavLink
              to={`/app/${organization.slug}/dashboard`}
              className={({ isActive }) =>
                `block rounded-md px-3 py-2 ${isActive ? "bg-slate-900 text-white" : "text-slate-700 hover:bg-slate-100"}`
              }
            >
              Dashboard
            </NavLink>
            <NavLink
              to={`/app/${organization.slug}/members`}
              className={({ isActive }) =>
                `block rounded-md px-3 py-2 ${isActive ? "bg-slate-900 text-white" : "text-slate-700 hover:bg-slate-100"}`
              }
            >
              Üyeler
            </NavLink>
            <NavLink
              to={`/app/${organization.slug}/teams`}
              className={({ isActive }) =>
                `block rounded-md px-3 py-2 ${isActive ? "bg-slate-900 text-white" : "text-slate-700 hover:bg-slate-100"}`
              }
            >
              Teams
            </NavLink>
            <NavLink
              to={`/app/${organization.slug}/projects`}
              className={({ isActive }) =>
                `block rounded-md px-3 py-2 ${isActive ? "bg-slate-900 text-white" : "text-slate-700 hover:bg-slate-100"}`
              }
            >
              Projects
            </NavLink>
            {canManageOrganization ? (
              <NavLink
                to={`/app/${organization.slug}/settings`}
                className={({ isActive }) =>
                  `block rounded-md px-3 py-2 ${isActive ? "bg-slate-900 text-white" : "text-slate-700 hover:bg-slate-100"}`
                }
              >
                Ayarlar
              </NavLink>
            ) : null}
            {canViewAudit ? <NavLink to={`/app/${organization.slug}/audit`} className={({ isActive }) => `block rounded-md px-3 py-2 ${isActive ? "bg-slate-900 text-white" : "text-slate-700 hover:bg-slate-100"}`}>Audit kayıtları</NavLink> : null}
          </nav>
        </aside>
        <main id="main-content" tabIndex={-1} className="min-w-0 flex-1 p-4 sm:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
