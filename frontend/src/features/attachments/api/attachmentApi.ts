import { apiClient } from "@/lib/api/apiClient";
import type { Attachment } from "./types";

export const attachmentApi = {
  list: (workItemId: string): Promise<Attachment[]> =>
    apiClient.get(`/work-items/${workItemId}/attachments`),
  upload: (workItemId: string, file: File, onProgress: (percent: number) => void): Promise<Attachment> =>
    apiClient.upload(`/work-items/${workItemId}/attachments`, file, onProgress),
  download: (attachment: Attachment): Promise<void> =>
    apiClient.download(`/attachments/${attachment.id}`, attachment.originalFileName),
  delete: (attachmentId: string): Promise<void> =>
    apiClient.delete(`/attachments/${attachmentId}`),
};
