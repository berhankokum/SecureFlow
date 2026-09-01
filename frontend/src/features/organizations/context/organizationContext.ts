import { createContext } from "react";
import type { Organization } from "../api/types";

export const OrganizationContext = createContext<Organization | null>(null);
