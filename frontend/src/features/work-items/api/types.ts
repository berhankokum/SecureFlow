import type { Permission } from "@/features/authorization/permissions";
import type { WorkItemLabel } from "@/features/labels/api/labelApi";

export type WorkItemType = "TASK" | "BUG" | "STORY";
export type WorkItemPriority = "LOW" | "MEDIUM" | "HIGH" | "CRITICAL";
export type WorkflowCategory = "TODO" | "IN_PROGRESS" | "DONE";

export interface WorkflowState {
  id: string;
  name: string;
  category: WorkflowCategory;
  position: number;
}

export interface WorkflowTransition {
  id: string;
  fromStateId: string;
  toStateId: string;
  requiredPermission: Permission | null;
}

export interface Workflow {
  id: string;
  organizationId: string;
  name: string;
  states: WorkflowState[];
  transitions: WorkflowTransition[];
}

export interface WorkItem {
  version?: number;
  id: string;
  organizationId: string;
  projectId: string;
  projectKey: string;
  number: number;
  key: string;
  type: WorkItemType;
  title: string;
  description: string | null;
  priority: WorkItemPriority;
  workflowStateId: string;
  workflowStateName: string;
  workflowStateCategory: WorkflowCategory;
  reporterId: string;
  reporterEmail: string;
  assigneeId: string | null;
  assigneeEmail: string | null;
  dueDate: string | null;
  createdAt: string;
  updatedAt: string;
  archivedAt: string | null;
  permissions: Permission[];
  labels: WorkItemLabel[];
}

export interface WorkItemPayload {
  type: WorkItemType;
  title: string;
  description?: string;
  priority: WorkItemPriority;
  dueDate?: string;
}
