import { Outlet, useNavigate } from "react-router-dom";
import { useCurrentUser } from "@/features/auth/hooks/useCurrentUser";
import { useLogout } from "@/features/auth/hooks/useLogout";

/**
 * FAZ 1 uygulama iskeleti. TopBar/Sidebar navigasyonu, ilgili
 * feature'lar (Organizations, Projects, ...) eklendikçe FAZ 3+'ta genişler.
 */
export function AppShell() {
  const { data: user } = useCurrentUser();
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
      <main className="flex-1 p-6">
        <Outlet />
      </main>
    </div>
  );
}
