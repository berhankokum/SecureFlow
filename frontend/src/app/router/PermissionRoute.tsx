import { Outlet } from "react-router-dom";
import { usePermission } from "@/features/authorization/hooks/usePermission";
import { ForbiddenPage } from "@/features/authorization/pages/ForbiddenPage";
import type { Permission } from "@/features/authorization/permissions";

interface PermissionRouteProps {
  permission: Permission;
}

export function PermissionRoute({ permission }: PermissionRouteProps) {
  return usePermission(permission) ? <Outlet /> : <ForbiddenPage />;
}
