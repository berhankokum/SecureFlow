import { useMutation } from "@tanstack/react-query";
import { resetPassword } from "../api/resetPassword";

export function useResetPassword() {
  return useMutation({ mutationFn: resetPassword });
}
