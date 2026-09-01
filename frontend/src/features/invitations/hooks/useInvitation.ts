import { useQuery } from "@tanstack/react-query";
import { getInvitation } from "../api/invitationApi";

export const invitationQueryKey = (token: string) => ["invitations", "token", token] as const;

export function useInvitation(token: string | undefined) {
  return useQuery({
    queryKey: invitationQueryKey(token ?? ""),
    queryFn: () => getInvitation(token!),
    enabled: Boolean(token),
    retry: false,
  });
}
