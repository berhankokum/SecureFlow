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
  const logoutMutation = useLogout();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logoutMutation.mutateAsync();
    navigate("/login", { replace: true });
  };

  return (
    <div className="flex min-h-screen flex-col bg-slate-50 text-slate-900">
      <header className="flex items-center justify-between border-b border-slate-200 bg-white px-6 py-4">
        <span className="text-lg font-semibold tracking-tight">SecureFlow</span>
        {user ? (
          <div className="flex items-center gap-4 text-sm text-slate-600">
            <span>{user.email}</span>
            <NotificationBell />
            <button
              type="button"
              onClick={handleLogout}
              disabled={logoutMutation.isPending}
              className="rounded-md border border-slate-300 px-3 py-1 text-sm hover:bg-slate-50 disabled:opacity-50"
            >
              Çıkış yap
            </button>
          </div>
        ) : null}
      </header>
      <div className="flex flex-1 flex-col md:flex-row">
        <aside className="w-full border-b border-slate-200 bg-white p-4 md:w-64 md:border-b-0 md:border-r">
          <OrganizationSwitcher />
          <nav className="mt-6 space-y-1 text-sm">
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
          </nav>
        </aside>
        <main className="flex-1 p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
