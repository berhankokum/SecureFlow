import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createInvitation } from "../api/invitationApi";
import type { CreateInvitationPayload } from "../api/types";
import { invitationListQueryKey } from "./useInvitations";

export function useCreateInvitation(organizationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: CreateInvitationPayload) => createInvitation(organizationId, payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: invitationListQueryKey(organizationId) });
    },
  });
}
