import { useMutation } from "@tanstack/react-query";
import { confirmEmail } from "../api/confirmEmail";

export function useConfirmEmail() {
  return useMutation({ mutationFn: confirmEmail });
}
