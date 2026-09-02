import type { Permission } from "@/features/authorization/permissions";

export type ProjectRole = "MANAGER" | "CONTRIBUTOR" | "VIEWER";

export interface ProjectSummary {
  id: string;
  organizationId: string;
  key: string;
  name: string;
  description: string | null;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
  archivedAt: string | null;
  projectRole: ProjectRole | null;
  permissions: Permission[];
}

export interface ProjectMember {
  userId: string;
  email: string;
  role: ProjectRole;
}

export interface ProjectDetails extends ProjectSummary {
  members: ProjectMember[];
}

export interface CreateProjectPayload {
  key: string;
  name: string;
  description?: string;
}

export interface UpdateProjectPayload {
  name: string;
  description?: string;
}
