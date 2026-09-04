import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { notificationApi } from "../api/notificationApi";
import type { NotificationPreference } from "../api/types";

export const notificationKeys = {
  list: ["notifications"] as const,
  preference: ["notifications", "preference"] as const,
};

export function useNotifications() {
  return useQuery({ queryKey: notificationKeys.list, queryFn: notificationApi.list });
}

export function useMarkNotificationRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: notificationApi.markRead,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: notificationKeys.list }),
  });
}

export function useMarkAllNotificationsRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: notificationApi.markAllRead,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: notificationKeys.list }),
  });
}

export function useNotificationPreference() {
  return useQuery({ queryKey: notificationKeys.preference, queryFn: notificationApi.getPreference });
}

export function useUpdateNotificationPreference() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: notificationApi.updatePreference,
    onMutate: async emailEnabled => {
      await queryClient.cancelQueries({ queryKey: notificationKeys.preference });
      const previous = queryClient.getQueryData<NotificationPreference>(notificationKeys.preference);
      queryClient.setQueryData<NotificationPreference>(notificationKeys.preference, current => ({
        inAppEnabled: true,
        emailEnabled,
        updatedAt: current?.updatedAt ?? null,
      }));
      return { previous };
    },
    onError: (_error, _emailEnabled, context) => {
      if (context?.previous) queryClient.setQueryData(notificationKeys.preference, context.previous);
    },
    onSuccess: preference => queryClient.setQueryData(notificationKeys.preference, preference),
    onSettled: () => queryClient.invalidateQueries({ queryKey: notificationKeys.preference }),
  });
}
