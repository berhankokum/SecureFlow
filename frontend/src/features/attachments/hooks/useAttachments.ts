import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { activityKeys } from "@/features/comments/hooks/useCollaboration";
import { attachmentApi } from "../api/attachmentApi";

export const attachmentKeys = {
  all: (workItemId: string) => ["attachments", workItemId] as const,
};

export function useAttachments(workItemId: string) {
  return useQuery({
    queryKey: attachmentKeys.all(workItemId),
    queryFn: () => attachmentApi.list(workItemId),
    enabled: workItemId.length > 0,
  });
}

export function useUploadAttachment(workItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ file, onProgress }: { file: File; onProgress: (percent: number) => void }) =>
      attachmentApi.upload(workItemId, file, onProgress),
    onSuccess: async () => Promise.all([
      queryClient.invalidateQueries({ queryKey: attachmentKeys.all(workItemId) }),
      queryClient.invalidateQueries({ queryKey: activityKeys.all(workItemId) }),
    ]),
  });
}

export function useDeleteAttachment(workItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (attachmentId: string) => attachmentApi.delete(attachmentId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: attachmentKeys.all(workItemId) }),
  });
}
