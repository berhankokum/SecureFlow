import { useCurrentOrganization } from "@/features/organizations/hooks/useCurrentOrganization";
import type { Permission } from "../permissions";

export function usePermission(permission: Permission) {
  const organization = useCurrentOrganization();
  return organization.permissions.includes(permission);
}
