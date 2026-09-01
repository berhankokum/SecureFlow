import { useContext } from "react";
import { OrganizationContext } from "../context/organizationContext";
import type { Organization } from "../api/types";

export function useCurrentOrganization(): Organization {
  const organization = useContext(OrganizationContext);
  if (!organization) {
    throw new Error("Organization context is not available for this route.");
  }

  return organization;
}
