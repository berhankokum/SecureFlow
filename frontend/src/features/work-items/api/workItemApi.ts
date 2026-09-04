import { apiClient } from "@/lib/api/apiClient";
import type { WorkItem, WorkItemPayload } from "./types";

export const workItemApi = {
  list: (projectId: string): Promise<WorkItem[]> => apiClient.get(`/projects/${projectId}/work-items`),
  get: (workItemId: string): Promise<WorkItem> => apiClient.get(`/work-items/${workItemId}`),
  create: (projectId: string, payload: WorkItemPayload): Promise<WorkItem> =>
    apiClient.post(`/projects/${projectId}/work-items`, payload),
  update: (workItemId: string, payload: WorkItemPayload): Promise<void> =>
    apiClient.patch(`/work-items/${workItemId}`, payload),
  archive: (workItemId: string): Promise<void> => apiClient.post(`/work-items/${workItemId}/archive`),
  assign: (workItemId: string, assigneeId: string): Promise<void> =>
    apiClient.post(`/work-items/${workItemId}/assign`, { assigneeId }),
  selfAssign: (workItemId: string): Promise<void> =>
    apiClient.post(`/work-items/${workItemId}/self-assign`),
  unassign: (workItemId: string): Promise<void> =>
    apiClient.delete(`/work-items/${workItemId}/assignment`),
};
