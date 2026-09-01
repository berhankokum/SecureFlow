import type { PropsWithChildren } from "react";
import type { Organization } from "../api/types";
import { OrganizationContext } from "./organizationContext";

interface OrganizationProviderProps extends PropsWithChildren {
  organization: Organization;
}

export function OrganizationProvider({ organization, children }: OrganizationProviderProps) {
  return (
    <OrganizationContext.Provider value={organization}>
      {children}
    </OrganizationContext.Provider>
  );
}
