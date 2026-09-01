import { useMutation, useQueryClient } from "@tanstack/react-query";
import { changeMemberRole } from "../api/changeMemberRole";
import type { OrganizationRole } from "../api/types";
import { memberQueryKey } from "./useMembers";

export function useChangeMemberRole(organizationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: OrganizationRole }) =>
      changeMemberRole(organizationId, userId, { role }),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: memberQueryKey(organizationId) }),
        queryClient.invalidateQueries({ queryKey: ["organizations"] }),
      ]);
    },
  });
}
