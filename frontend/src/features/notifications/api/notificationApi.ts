import { apiClient } from "@/lib/api/apiClient";
import type { NotificationList, NotificationPreference } from "./types";

export const notificationApi = {
  list: (): Promise<NotificationList> => apiClient.get("/notifications"),
  markRead: (notificationId: string): Promise<void> =>
    apiClient.post(`/notifications/${notificationId}/read`),
  markAllRead: (): Promise<void> => apiClient.post("/notifications/read-all"),
  getPreference: (): Promise<NotificationPreference> =>
    apiClient.get("/notifications/preferences"),
  updatePreference: (emailEnabled: boolean): Promise<NotificationPreference> =>
    apiClient.put("/notifications/preferences", { emailEnabled }),
};
