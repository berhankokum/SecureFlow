import { useQuery } from "@tanstack/react-query";
import { getInvitations } from "../api/invitationApi";

export const invitationListQueryKey = (organizationId: string) =>
  ["invitations", "organization", organizationId] as const;

export function useInvitations(organizationId: string, enabled = true) {
  return useQuery({
    queryKey: invitationListQueryKey(organizationId),
    queryFn: () => getInvitations(organizationId),
    enabled,
  });
}
