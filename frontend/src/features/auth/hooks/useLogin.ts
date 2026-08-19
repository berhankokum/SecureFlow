import { useMutation, useQueryClient } from "@tanstack/react-query";
import { refreshCsrfToken } from "@/lib/api/apiClient";
import { login } from "../api/login";
import { currentUserQueryKey } from "./useCurrentUser";

export function useLogin() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: login,
    onSuccess: async () => {
      // Principal değişti: eski antiforgery token artık geçersiz.
      await refreshCsrfToken();
      await queryClient.invalidateQueries({ queryKey: currentUserQueryKey });
    },
  });
}
