export interface Attachment {
  id: string;
  organizationId: string;
  workItemId: string;
  originalFileName: string;
  contentType: string;
  size: number;
  uploadedBy: string;
  uploadedByEmail: string;
  uploadedAt: string;
  canDelete: boolean;
}
