import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { activityKeys } from "@/features/comments/hooks/useCollaboration";
import { labelApi } from "../api/labelApi";

export const labelKeys = { all: (projectId: string) => ["labels", projectId] as const };

export function useLabels(projectId: string) {
  return useQuery({ queryKey: labelKeys.all(projectId), queryFn: () => labelApi.list(projectId), enabled: !!projectId });
}

function useInvalidateLabels(projectId: string, workItemId?: string) {
  const client = useQueryClient();
  return () => Promise.all([
    client.invalidateQueries({ queryKey: labelKeys.all(projectId) }),
    // A rename/delete also changes every cached WorkItem response containing that label.
    client.invalidateQueries({ queryKey: ["work-items"] }),
    ...(workItemId
      ? [client.invalidateQueries({ queryKey: activityKeys.all(workItemId) })]
      : []),
  ]);
}

export function useCreateLabel(projectId: string) {
  const invalidate = useInvalidateLabels(projectId);
  return useMutation({ mutationFn: (name: string) => labelApi.create(projectId, name), onSuccess: invalidate });
}

export function useUpdateLabel(projectId: string) {
  const invalidate = useInvalidateLabels(projectId);
  return useMutation({ mutationFn: ({ id, name }: { id: string; name: string }) => labelApi.update(id, name), onSuccess: invalidate });
}

export function useDeleteLabel(projectId: string) {
  const invalidate = useInvalidateLabels(projectId);
  return useMutation({ mutationFn: labelApi.delete, onSuccess: invalidate });
}

export function useSetWorkItemLabel(projectId: string, workItemId: string) {
  const invalidate = useInvalidateLabels(projectId, workItemId);
  return useMutation({
    mutationFn: ({ labelId, assigned }: { labelId: string; assigned: boolean }) =>
      assigned ? labelApi.assign(workItemId, labelId) : labelApi.remove(workItemId, labelId),
    onSuccess: invalidate,
  });
}
