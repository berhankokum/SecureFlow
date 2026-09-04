import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { workItemApi } from "../api/workItemApi";
import type { WorkItemPayload } from "../api/types";

export const workItemKeys = {
  all: (projectId: string) => ["work-items", projectId] as const,
  detail: (workItemId: string) => ["work-items", "detail", workItemId] as const,
};

export function useWorkItems(projectId: string) {
  return useQuery({
    queryKey: workItemKeys.all(projectId),
    queryFn: () => workItemApi.list(projectId),
    enabled: projectId.length > 0,
  });
}

export function useWorkItem(workItemId: string) {
  return useQuery({
    queryKey: workItemKeys.detail(workItemId),
    queryFn: () => workItemApi.get(workItemId),
    enabled: workItemId.length > 0,
  });
}

export function useCreateWorkItem(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: WorkItemPayload) => workItemApi.create(projectId, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: workItemKeys.all(projectId) }),
  });
}

function useInvalidateWorkItem(projectId: string, workItemId: string) {
  const queryClient = useQueryClient();
  return async () => Promise.all([
    queryClient.invalidateQueries({ queryKey: workItemKeys.all(projectId) }),
    queryClient.invalidateQueries({ queryKey: workItemKeys.detail(workItemId) }),
  ]);
}

export function useUpdateWorkItem(projectId: string, workItemId: string) {
  const invalidate = useInvalidateWorkItem(projectId, workItemId);
  return useMutation({
    mutationFn: (payload: WorkItemPayload) => workItemApi.update(workItemId, payload),
    onSuccess: invalidate,
  });
}

export function useArchiveWorkItem(projectId: string, workItemId: string) {
  const invalidate = useInvalidateWorkItem(projectId, workItemId);
  return useMutation({ mutationFn: () => workItemApi.archive(workItemId), onSuccess: invalidate });
}

export function useAssignWorkItem(projectId: string, workItemId: string) {
  const invalidate = useInvalidateWorkItem(projectId, workItemId);
  return useMutation({
    mutationFn: (assigneeId: string) => workItemApi.assign(workItemId, assigneeId),
    onSuccess: invalidate,
  });
}

export function useSelfAssignWorkItem(projectId: string, workItemId: string) {
  const invalidate = useInvalidateWorkItem(projectId, workItemId);
  return useMutation({ mutationFn: () => workItemApi.selfAssign(workItemId), onSuccess: invalidate });
}

export function useUnassignWorkItem(projectId: string, workItemId: string) {
  const invalidate = useInvalidateWorkItem(projectId, workItemId);
  return useMutation({ mutationFn: () => workItemApi.unassign(workItemId), onSuccess: invalidate });
}
