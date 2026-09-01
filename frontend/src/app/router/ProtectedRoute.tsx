import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useCurrentUser } from "@/features/auth/hooks/useCurrentUser";

/**
 * SecureFlow-Final.md §48: frontend permission/route koruması yalnızca UX'tir.
 * Gerçek güvenlik her zaman backend'de (burada: /me üzerindeki [Authorize]) sağlanır.
 */
export function ProtectedRoute() {
  const { data: user, isLoading, isError } = useCurrentUser();
  const location = useLocation();

  if (isLoading) {
    return <div className="p-6 text-sm text-slate-500">Yükleniyor...</div>;
  }

  if (isError || !user) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return <Outlet />;
}
