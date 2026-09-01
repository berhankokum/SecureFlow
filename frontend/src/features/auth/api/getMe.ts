import { apiClient } from "@/lib/api/apiClient";
import type { CurrentUser } from "./types";

export function getMe(): Promise<CurrentUser> {
  return apiClient.get<CurrentUser>("/me");
}
