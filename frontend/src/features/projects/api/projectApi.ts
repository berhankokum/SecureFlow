import { apiClient } from "@/lib/api/apiClient";
import type { CreateProjectPayload, ProjectDetails, ProjectRole, ProjectSummary, UpdateProjectPayload } from "./types";

export const projectApi = {
  list: (organizationId: string): Promise<ProjectSummary[]> =>
    apiClient.get(`/organizations/${organizationId}/projects`),
  get: (projectId: string): Promise<ProjectDetails> => apiClient.get(`/projects/${projectId}`),
  create: (organizationId: string, payload: CreateProjectPayload): Promise<ProjectSummary> =>
    apiClient.post(`/organizations/${organizationId}/projects`, payload),
  update: (projectId: string, payload: UpdateProjectPayload): Promise<void> =>
    apiClient.patch(`/projects/${projectId}`, payload),
  archive: (projectId: string): Promise<void> => apiClient.post(`/projects/${projectId}/archive`),
  addMember: (projectId: string, userId: string, role: ProjectRole): Promise<void> =>
    apiClient.post(`/projects/${projectId}/members`, { userId, role }),
  changeMemberRole: (projectId: string, userId: string, role: ProjectRole): Promise<void> =>
    apiClient.patch(`/projects/${projectId}/members/${userId}`, { role }),
  removeMember: (projectId: string, userId: string): Promise<void> =>
    apiClient.delete(`/projects/${projectId}/members/${userId}`),
};
