import { useMutation, useQueryClient } from "@tanstack/react-query";
import { refreshCsrfToken } from "@/lib/api/apiClient";
import { logout } from "../api/logout";
import { currentUserQueryKey } from "./useCurrentUser";

export function useLogout() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: logout,
    onSuccess: async () => {
      await refreshCsrfToken();
      queryClient.removeQueries({ queryKey: currentUserQueryKey });
    },
  });
}
