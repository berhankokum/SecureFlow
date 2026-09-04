export type NotificationType =
  | "WORK_ITEM_ASSIGNED"
  | "WORK_ITEM_STATUS_CHANGED"
  | "MENTIONED"
  | "COMMENT_ADDED"
  | "DUE_DATE_APPROACHING"
  | "INVITATION_RECEIVED";

export interface NotificationItem {
  id: string;
  organizationId: string;
  type: NotificationType;
  title: string;
  message: string;
  resourceType: "WORK_ITEM" | "ORGANIZATION_INVITATION";
  resourceId: string | null;
  isRead: boolean;
  createdAt: string;
  readAt: string | null;
}

export interface NotificationList {
  items: NotificationItem[];
  unreadCount: number;
}

export interface NotificationPreference {
  inAppEnabled: true;
  emailEnabled: boolean;
  updatedAt: string | null;
}
