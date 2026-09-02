import { apiClient } from "@/lib/api/apiClient";
import type { TeamDetails, TeamPayload, TeamSummary } from "./types";

export const teamApi = {
  list: (organizationId: string): Promise<TeamSummary[]> =>
    apiClient.get(`/organizations/${organizationId}/teams`),

  get: (teamId: string): Promise<TeamDetails> => apiClient.get(`/teams/${teamId}`),

  create: (organizationId: string, payload: TeamPayload): Promise<TeamSummary> =>
    apiClient.post(`/organizations/${organizationId}/teams`, payload),

  update: (teamId: string, payload: TeamPayload): Promise<void> =>
    apiClient.patch(`/teams/${teamId}`, payload),

  archive: (teamId: string): Promise<void> => apiClient.post(`/teams/${teamId}/archive`),

  addMember: (teamId: string, userId: string): Promise<void> =>
    apiClient.post(`/teams/${teamId}/members/${userId}`),

  removeMember: (teamId: string, userId: string): Promise<void> =>
    apiClient.delete(`/teams/${teamId}/members/${userId}`),
};
