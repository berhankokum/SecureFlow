import { apiClient } from "@/lib/api/apiClient";
import type { ActivityEvent, Comment } from "./types";

export const collaborationApi = {
  listComments: (workItemId: string): Promise<Comment[]> =>
    apiClient.get(`/work-items/${workItemId}/comments`),
  createComment: (workItemId: string, content: string): Promise<Comment> =>
    apiClient.post(`/work-items/${workItemId}/comments`, { content }),
  updateComment: (commentId: string, content: string): Promise<void> =>
    apiClient.patch(`/comments/${commentId}`, { content }),
  deleteComment: (commentId: string): Promise<void> =>
    apiClient.delete(`/comments/${commentId}`),
  listActivity: (workItemId: string): Promise<ActivityEvent[]> =>
    apiClient.get(`/work-items/${workItemId}/activity`),
};
