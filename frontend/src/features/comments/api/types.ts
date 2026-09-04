export interface Comment {
  id: string;
  organizationId: string;
  workItemId: string;
  authorId: string;
  authorEmail: string;
  content: string | null;
  createdAt: string;
  updatedAt: string;
  deletedAt: string | null;
  canUpdate: boolean;
  canDelete: boolean;
}

export type ActivityType =
  | "WORK_ITEM_CREATED"
  | "WORK_ITEM_ASSIGNED"
  | "WORK_ITEM_UNASSIGNED"
  | "WORK_ITEM_TRANSITIONED"
  | "LABEL_ASSIGNED"
  | "LABEL_REMOVED"
  | "COMMENT_CREATED";

export interface ActivityEvent {
  id: string;
  organizationId: string;
  projectId: string | null;
  workItemId: string | null;
  actorId: string;
  actorEmail: string;
  type: ActivityType;
  metadata: Record<string, string | null>;
  createdAt: string;
}
