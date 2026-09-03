import type { Permission } from "@/features/authorization/permissions";
import type { WorkItemLabel } from "@/features/labels/api/labelApi";

export type WorkItemType = "TASK" | "BUG" | "STORY";
export type WorkItemPriority = "LOW" | "MEDIUM" | "HIGH" | "CRITICAL";

export interface WorkItem {
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
  reporterId: string;
  reporterEmail: string;
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
