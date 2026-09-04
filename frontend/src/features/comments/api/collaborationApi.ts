import { apiClient } from "@/lib/api/apiClient";
import type { ActivityEvent, Comment, CommentPayload } from "./types";

export const collaborationApi = {
  listComments: (workItemId: string): Promise<Comment[]> =>
    apiClient.get(`/work-items/${workItemId}/comments`),
  createComment: (workItemId: string, payload: CommentPayload): Promise<Comment> =>
    apiClient.post(`/work-items/${workItemId}/comments`, payload),
  updateComment: (commentId: string, payload: CommentPayload): Promise<void> =>
    apiClient.patch(`/comments/${commentId}`, payload),
  deleteComment: (commentId: string): Promise<void> =>
    apiClient.delete(`/comments/${commentId}`),
  listActivity: (workItemId: string): Promise<ActivityEvent[]> =>
    apiClient.get(`/work-items/${workItemId}/activity`),
};
