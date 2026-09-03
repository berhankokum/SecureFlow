import { useQuery } from "@tanstack/react-query";
import { getMembers } from "../api/getMembers";

export const memberQueryKey = (organizationId: string) =>
  ["memberships", organizationId] as const;

export function useMembers(organizationId: string, enabled = true) {
  return useQuery({
    queryKey: memberQueryKey(organizationId),
    queryFn: () => getMembers(organizationId),
    enabled: enabled && organizationId.length > 0,
  });
}
