import { Navigate, Outlet, useLocation } from "react-router-dom";
import { LoadingState } from "@/components/ui/LoadingState";
import { useCurrentUser } from "@/features/auth/hooks/useCurrentUser";

/**
 * SecureFlow-Final.md §48: frontend permission/route koruması yalnızca UX'tir.
 * Gerçek güvenlik her zaman backend'de (burada: /me üzerindeki [Authorize]) sağlanır.
 */
export function ProtectedRoute() {
  const { data: user, isLoading, isError } = useCurrentUser();
  const location = useLocation();

  if (isLoading) {
    return <LoadingState label="Yükleniyor..." />;
  }

  if (isError || !user) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return <Outlet />;
}
