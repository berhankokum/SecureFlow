import { useMutation, useQueryClient } from "@tanstack/react-query";
import { acceptInvitation } from "../api/invitationApi";

export function useAcceptInvitation(token: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => acceptInvitation(token),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["organizations"] }),
        queryClient.invalidateQueries({ queryKey: ["memberships"] }),
        queryClient.invalidateQueries({ queryKey: ["invitations"] }),
      ]);
    },
  });
}
