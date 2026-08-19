import { useMutation } from "@tanstack/react-query";
import { forgotPassword } from "../api/forgotPassword";

export function useForgotPassword() {
  return useMutation({ mutationFn: forgotPassword });
}
