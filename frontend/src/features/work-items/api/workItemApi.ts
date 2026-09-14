import { apiClient } from "@/lib/api/apiClient";
import type { WorkItem, WorkItemPayload, Workflow } from "./types";

export const workItemApi = {
  list: (projectId: string, query = ""): Promise<WorkItem[]> => apiClient.get(`/projects/${projectId}/work-items${query ? `?${query}` : ""}`),
  get: (workItemId: string): Promise<WorkItem> => apiClient.get(`/work-items/${workItemId}`),
  getWorkflow: (projectId: string): Promise<Workflow> => apiClient.get(`/projects/${projectId}/workflow`),
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
  transition: (workItemId: string, toStateId: string): Promise<void> =>
    apiClient.post(`/work-items/${workItemId}/transition`, { toStateId }),
};
