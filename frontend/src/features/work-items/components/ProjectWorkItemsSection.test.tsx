import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ProjectDetails } from "@/features/projects/api/types";
import { renderWithProviders } from "@/test/renderWithProviders";
import { ProjectWorkItemsSection } from "./ProjectWorkItemsSection";

const create = vi.fn();
const project: ProjectDetails = {
  id: "project-1",
  organizationId: "organization-1",
  key: "SF",
  name: "SecureFlow",
  description: null,
  createdBy: "owner",
  createdAt: "2026-09-02T00:00:00Z",
  updatedAt: "2026-09-02T00:00:00Z",
  archivedAt: null,
  projectRole: "MANAGER",
  permissions: ["workitem.create", "workitem.update", "workitem.archive"],
  members: [],
};

vi.mock("../hooks/useWorkItems", () => ({
  useWorkItems: () => ({ data: [{
    id: "workitem-1", organizationId: "organization-1", projectId: "project-1",
    projectKey: "SF", number: 1, key: "SF-1", type: "BUG", title: "Tenant isolation",
    description: null, priority: "CRITICAL", reporterId: "owner", reporterEmail: "owner@example.com", assigneeId: "owner", assigneeEmail: "owner@example.com", dueDate: null,
    createdAt: "2026-09-02T00:00:00Z", updatedAt: "2026-09-02T00:00:00Z", archivedAt: null,
    permissions: ["workitem.update"], labels: [{ id: "label-1", name: "Security", assignedBy: "owner", assignedAt: "2026-09-02T00:00:00Z" }],
  }], isLoading: false, isError: false }),
  useCreateWorkItem: () => ({ mutateAsync: create }),
}));
vi.mock("@/features/auth/hooks/useCurrentUser", () => ({
  useCurrentUser: () => ({ data: { id: "owner", email: "owner@example.com" } }),
}));

describe("ProjectWorkItemsSection", () => {
  beforeEach(() => create.mockReset().mockResolvedValue({ id: "workitem-2" }));

  it("lists and creates WorkItems when permission is present", async () => {
    const user = userEvent.setup();
    renderWithProviders(<ProjectWorkItemsSection project={project} />);

    expect(screen.getByText("SF-1")).toBeInTheDocument();
    expect(screen.getByText("Tenant isolation")).toBeInTheDocument();
    expect(screen.getByText("Security")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "WorkItem oluştur" }));
    await user.selectOptions(screen.getByLabelText("Tür"), "STORY");
    await user.type(screen.getByLabelText("Başlık"), "Permission-aware UI");
    await user.selectOptions(screen.getByLabelText("Öncelik"), "HIGH");
    await user.click(screen.getAllByRole("button", { name: "WorkItem oluştur" }).at(-1)!);

    expect(create).toHaveBeenCalledWith({
      type: "STORY",
      title: "Permission-aware UI",
      description: undefined,
      priority: "HIGH",
      dueDate: undefined,
    });
  });

  it("hides create action for a read-only project role", () => {
    renderWithProviders(<ProjectWorkItemsSection project={{ ...project, permissions: [] }} />);
    expect(screen.queryByRole("button", { name: "WorkItem oluştur" })).not.toBeInTheDocument();
  });

  it("filters My Work using the server-owned assignee id", async () => {
    const user = userEvent.setup();
    renderWithProviders(<ProjectWorkItemsSection project={project} />);
    await user.click(screen.getByRole("button", { name: "Bana atananlar" }));
    expect(screen.getByText("Tenant isolation")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Bana atananlar" })).toHaveAttribute("aria-pressed", "true");
  });
});
