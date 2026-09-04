import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { collaborationApi } from "../api/collaborationApi";

export const commentKeys = {
  all: (workItemId: string) => ["comments", workItemId] as const,
};

export const activityKeys = {
  all: (workItemId: string) => ["activity", workItemId] as const,
};

export function useComments(workItemId: string) {
  return useQuery({
    queryKey: commentKeys.all(workItemId),
    queryFn: () => collaborationApi.listComments(workItemId),
    enabled: workItemId.length > 0,
  });
}

export function useActivity(workItemId: string) {
  return useQuery({
    queryKey: activityKeys.all(workItemId),
    queryFn: () => collaborationApi.listActivity(workItemId),
    enabled: workItemId.length > 0,
  });
}

export function useCreateComment(workItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (content: string) => collaborationApi.createComment(workItemId, content),
    onSuccess: async () => Promise.all([
      queryClient.invalidateQueries({ queryKey: commentKeys.all(workItemId) }),
      queryClient.invalidateQueries({ queryKey: activityKeys.all(workItemId) }),
    ]),
  });
}

export function useUpdateComment(workItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ commentId, content }: { commentId: string; content: string }) =>
      collaborationApi.updateComment(commentId, content),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: commentKeys.all(workItemId) }),
  });
}

export function useDeleteComment(workItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (commentId: string) => collaborationApi.deleteComment(commentId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: commentKeys.all(workItemId) }),
  });
}
