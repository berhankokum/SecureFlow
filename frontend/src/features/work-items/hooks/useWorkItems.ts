import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { workItemApi } from "../api/workItemApi";
import type { WorkItem, WorkItemPayload, WorkflowState } from "../api/types";

export const workItemKeys = {
  all: (projectId: string) => ["work-items", projectId] as const,
  detail: (workItemId: string) => ["work-items", "detail", workItemId] as const,
};

export const workflowKeys = {
  detail: (projectId: string) => ["workflows", projectId] as const,
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

export function useWorkflow(projectId: string) {
  return useQuery({
    queryKey: workflowKeys.detail(projectId),
    queryFn: () => workItemApi.getWorkflow(projectId),
    enabled: projectId.length > 0,
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

interface TransitionVariables {
  workItemId: string;
  toState: WorkflowState;
}

interface TransitionContext {
  previousList: WorkItem[] | undefined;
  previousDetail: WorkItem | undefined;
}

export function useTransitionWorkItem(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation<void, Error, TransitionVariables, TransitionContext>({
    mutationFn: ({ workItemId, toState }) => workItemApi.transition(workItemId, toState.id),
    onMutate: async ({ workItemId, toState }) => {
      await Promise.all([
        queryClient.cancelQueries({ queryKey: workItemKeys.all(projectId) }),
        queryClient.cancelQueries({ queryKey: workItemKeys.detail(workItemId) }),
      ]);
      const previousList = queryClient.getQueryData<WorkItem[]>(workItemKeys.all(projectId));
      const previousDetail = queryClient.getQueryData<WorkItem>(workItemKeys.detail(workItemId));
      const applyState = (item: WorkItem): WorkItem => item.id === workItemId
        ? {
            ...item,
            workflowStateId: toState.id,
            workflowStateName: toState.name,
            workflowStateCategory: toState.category,
          }
        : item;
      queryClient.setQueryData<WorkItem[]>(
        workItemKeys.all(projectId),
        current => current?.map(applyState),
      );
      queryClient.setQueryData<WorkItem>(
        workItemKeys.detail(workItemId),
        current => current ? applyState(current) : current,
      );
      return { previousList, previousDetail };
    },
    onError: (_error, { workItemId }, context) => {
      if (context?.previousList) {
        queryClient.setQueryData(workItemKeys.all(projectId), context.previousList);
      }
      if (context?.previousDetail) {
        queryClient.setQueryData(workItemKeys.detail(workItemId), context.previousDetail);
      }
    },
    onSettled: async (_data, _error, { workItemId }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: workItemKeys.all(projectId) }),
        queryClient.invalidateQueries({ queryKey: workItemKeys.detail(workItemId) }),
      ]);
    },
  });
}
