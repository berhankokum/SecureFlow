import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { projectApi } from "../api/projectApi";
import type { CreateProjectPayload, ProjectRole, UpdateProjectPayload } from "../api/types";
import { organizationsQueryKey } from "@/features/organizations/hooks/useOrganizations";

export const projectKeys = {
  all: (organizationId: string) => ["projects", organizationId] as const,
  detail: (projectId: string) => ["projects", "detail", projectId] as const,
};

export function useProjects(organizationId: string) {
  return useQuery({ queryKey: projectKeys.all(organizationId), queryFn: () => projectApi.list(organizationId) });
}

export function useProject(projectId: string) {
  return useQuery({ queryKey: projectKeys.detail(projectId), queryFn: () => projectApi.get(projectId), enabled: projectId.length > 0 });
}

export function useCreateProject(organizationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: CreateProjectPayload) => projectApi.create(organizationId, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectKeys.all(organizationId) }),
  });
}

function useProjectMutation(organizationId: string, projectId: string) {
  const queryClient = useQueryClient();
  return async () => Promise.all([
    queryClient.invalidateQueries({ queryKey: projectKeys.all(organizationId) }),
    queryClient.invalidateQueries({ queryKey: projectKeys.detail(projectId) }),
    queryClient.invalidateQueries({ queryKey: organizationsQueryKey }),
  ]);
}

export function useUpdateProject(organizationId: string, projectId: string) {
  const invalidate = useProjectMutation(organizationId, projectId);
  return useMutation({ mutationFn: (payload: UpdateProjectPayload) => projectApi.update(projectId, payload), onSuccess: invalidate });
}

export function useArchiveProject(organizationId: string, projectId: string) {
  const invalidate = useProjectMutation(organizationId, projectId);
  return useMutation({ mutationFn: () => projectApi.archive(projectId), onSuccess: invalidate });
}

export function useAddProjectMember(organizationId: string, projectId: string) {
  const invalidate = useProjectMutation(organizationId, projectId);
  return useMutation({ mutationFn: ({ userId, role }: { userId: string; role: ProjectRole }) => projectApi.addMember(projectId, userId, role), onSuccess: invalidate });
}

export function useChangeProjectMemberRole(organizationId: string, projectId: string) {
  const invalidate = useProjectMutation(organizationId, projectId);
  return useMutation({ mutationFn: ({ userId, role }: { userId: string; role: ProjectRole }) => projectApi.changeMemberRole(projectId, userId, role), onSuccess: invalidate });
}

export function useRemoveProjectMember(organizationId: string, projectId: string) {
  const invalidate = useProjectMutation(organizationId, projectId);
  return useMutation({ mutationFn: (userId: string) => projectApi.removeMember(projectId, userId), onSuccess: invalidate });
}
