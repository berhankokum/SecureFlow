import { useQuery } from "@tanstack/react-query";
import { getMembers } from "../api/getMembers";

export const memberQueryKey = (organizationId: string) =>
  ["memberships", organizationId] as const;

export function useMembers(organizationId: string) {
  return useQuery({
    queryKey: memberQueryKey(organizationId),
    queryFn: () => getMembers(organizationId),
  });
}
