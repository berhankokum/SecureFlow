import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { teamApi } from "../api/teamApi";
import type { TeamPayload } from "../api/types";

export const teamKeys = {
  all: (organizationId: string) => ["teams", organizationId] as const,
  detail: (teamId: string) => ["teams", "detail", teamId] as const,
};

export function useTeams(organizationId: string) {
  return useQuery({
    queryKey: teamKeys.all(organizationId),
    queryFn: () => teamApi.list(organizationId),
  });
}

export function useTeam(teamId: string) {
  return useQuery({
    queryKey: teamKeys.detail(teamId),
    queryFn: () => teamApi.get(teamId),
    enabled: teamId.length > 0,
  });
}

export function useCreateTeam(organizationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: TeamPayload) => teamApi.create(organizationId, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: teamKeys.all(organizationId) }),
  });
}

export function useUpdateTeam(organizationId: string, teamId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: TeamPayload) => teamApi.update(teamId, payload),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: teamKeys.all(organizationId) }),
        queryClient.invalidateQueries({ queryKey: teamKeys.detail(teamId) }),
      ]);
    },
  });
}

export function useArchiveTeam(organizationId: string, teamId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => teamApi.archive(teamId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: teamKeys.all(organizationId) }),
        queryClient.invalidateQueries({ queryKey: teamKeys.detail(teamId) }),
      ]);
    },
  });
}

export function useAddTeamMember(teamId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => teamApi.addMember(teamId, userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: teamKeys.detail(teamId) }),
  });
}

export function useRemoveTeamMember(teamId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) => teamApi.removeMember(teamId, userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: teamKeys.detail(teamId) }),
  });
}
