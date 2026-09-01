import { useMutation, useQueryClient } from "@tanstack/react-query";
import { revokeInvitation } from "../api/invitationApi";
import { invitationListQueryKey } from "./useInvitations";

export function useRevokeInvitation(organizationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (invitationId: string) => revokeInvitation(organizationId, invitationId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: invitationListQueryKey(organizationId) });
    },
  });
}
