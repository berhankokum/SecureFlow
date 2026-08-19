import { useQuery } from "@tanstack/react-query";
import { getMe } from "../api/getMe";

export const currentUserQueryKey = ["me"] as const;

export function useCurrentUser() {
  return useQuery({
    queryKey: currentUserQueryKey,
    queryFn: getMe,
    retry: false,
  });
}
