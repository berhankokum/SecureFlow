import { useMutation, useQueryClient } from "@tanstack/react-query";
import { removeMember } from "../api/removeMember";
import { memberQueryKey } from "./useMembers";

export function useRemoveMember(organizationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (userId: string) => removeMember(organizationId, userId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: memberQueryKey(organizationId) }),
        queryClient.invalidateQueries({ queryKey: ["organizations"] }),
      ]);
    },
  });
}
