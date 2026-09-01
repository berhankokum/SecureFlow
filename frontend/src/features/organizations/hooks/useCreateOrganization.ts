import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createOrganization } from "../api/createOrganization";
import { organizationsQueryKey } from "./useOrganizations";

export function useCreateOrganization() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createOrganization,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: organizationsQueryKey });
    },
  });
}
