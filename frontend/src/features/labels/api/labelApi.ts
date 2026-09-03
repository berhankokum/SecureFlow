import { apiClient } from "@/lib/api/apiClient";

export interface Label {
  id: string;
  organizationId: string;
  projectId: string;
  name: string;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
}

export interface WorkItemLabel {
  id: string;
  name: string;
  assignedBy: string;
  assignedAt: string;
}

export const labelApi = {
  list: (projectId: string): Promise<Label[]> => apiClient.get(`/projects/${projectId}/labels`),
  create: (projectId: string, name: string): Promise<Label> => apiClient.post(`/projects/${projectId}/labels`, { name }),
  update: (labelId: string, name: string): Promise<void> => apiClient.patch(`/labels/${labelId}`, { name }),
  delete: (labelId: string): Promise<void> => apiClient.delete(`/labels/${labelId}`),
  assign: (workItemId: string, labelId: string): Promise<void> => apiClient.post(`/work-items/${workItemId}/labels/${labelId}`),
  remove: (workItemId: string, labelId: string): Promise<void> => apiClient.delete(`/work-items/${workItemId}/labels/${labelId}`),
};
