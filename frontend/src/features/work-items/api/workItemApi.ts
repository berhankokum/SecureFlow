import { apiClient } from "@/lib/api/apiClient";
import type { WorkItem, WorkItemPayload, Workflow } from "./types";

export const workItemApi = {
  list: (projectId: string, query = ""): Promise<WorkItem[]> => apiClient.get(`/projects/${projectId}/work-items${query ? `?${query}` : ""}`),
  get: (workItemId: string): Promise<WorkItem> => apiClient.get(`/work-items/${workItemId}`),
  getWorkflow: (projectId: string): Promise<Workflow> => apiClient.get(`/projects/${projectId}/workflow`),
  create: (projectId: string, payload: WorkItemPayload): Promise<WorkItem> =>
    apiClient.post(`/projects/${projectId}/work-items`, payload),
  update: (workItemId: string, payload: WorkItemPayload, version?: number): Promise<void> =>
    apiClient.patch(`/work-items/${workItemId}`, payload, version),
  archive: (workItemId: string, version?: number): Promise<void> => apiClient.post(`/work-items/${workItemId}/archive`, undefined, version),
  assign: (workItemId: string, assigneeId: string, version?: number): Promise<void> =>
    apiClient.post(`/work-items/${workItemId}/assign`, { assigneeId }, version),
  selfAssign: (workItemId: string, version?: number): Promise<void> =>
    apiClient.post(`/work-items/${workItemId}/self-assign`, undefined, version),
  unassign: (workItemId: string, version?: number): Promise<void> =>
    apiClient.delete(`/work-items/${workItemId}/assignment`, version),
  transition: (workItemId: string, toStateId: string, version?: number): Promise<void> =>
    apiClient.post(`/work-items/${workItemId}/transition`, { toStateId }, version),
};
