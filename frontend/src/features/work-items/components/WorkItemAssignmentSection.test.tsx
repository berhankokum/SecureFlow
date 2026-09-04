import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ProjectDetails } from "@/features/projects/api/types";
import { renderWithProviders } from "@/test/renderWithProviders";
import type { WorkItem } from "../api/types";
import { WorkItemAssignmentSection } from "./WorkItemAssignmentSection";

const assign = vi.fn(), selfAssign = vi.fn(), unassign = vi.fn();
vi.mock("@/features/auth/hooks/useCurrentUser", () => ({ useCurrentUser: () => ({ data: { id: "current", email: "current@example.com" } }) }));
vi.mock("@/features/organizations/hooks/useCurrentOrganization", () => ({ useCurrentOrganization: () => ({ id: "org", role: "MEMBER" }) }));
vi.mock("@/features/memberships/hooks/useMembers", () => ({ useMembers: () => ({ data: [], isLoading: false }) }));
vi.mock("../hooks/useWorkItems", () => ({
  useAssignWorkItem: () => ({ mutateAsync: assign, isPending: false }),
  useSelfAssignWorkItem: () => ({ mutateAsync: selfAssign, isPending: false }),
  useUnassignWorkItem: () => ({ mutateAsync: unassign, isPending: false }),
}));

const time = "2026-09-04T00:00:00Z";
const project: ProjectDetails = {
  id: "project", organizationId: "org", key: "SF", name: "SecureFlow", description: null,
  createdBy: "owner", createdAt: time, updatedAt: time, archivedAt: null, projectRole: "MANAGER",
  permissions: ["workitem.assign", "workitem.self_assign"],
  members: [{ userId: "current", email: "current@example.com", role: "MANAGER" }, { userId: "target", email: "target@example.com", role: "CONTRIBUTOR" }],
};
const item: WorkItem = {
  id: "item", organizationId: "org", projectId: "project", projectKey: "SF", number: 1, key: "SF-1",
  type: "TASK", title: "Assignment", description: null, priority: "MEDIUM", reporterId: "owner",
  reporterEmail: "owner@example.com", assigneeId: null, assigneeEmail: null, dueDate: null,
  createdAt: time, updatedAt: time, archivedAt: null,
  permissions: ["workitem.assign", "workitem.self_assign"], labels: [],
};

describe("WorkItemAssignmentSection", () => {
  beforeEach(() => { for (const mock of [assign, selfAssign, unassign]) mock.mockReset().mockResolvedValue(undefined); });

  it("lets a manager assign another user and self-assign", async () => {
    const user = userEvent.setup();
    renderWithProviders(<WorkItemAssignmentSection workItem={item} project={project} />);
    expect(screen.getByText("Unassigned")).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText("Assignee"), "target");
    await user.click(screen.getByRole("button", { name: "Ata" }));
    expect(assign).toHaveBeenCalledWith("target");
    await user.click(screen.getByRole("button", { name: "Kendime ata" }));
    expect(selfAssign).toHaveBeenCalledOnce();
  });

  it("lets a contributor remove only their own assignment", async () => {
    const user = userEvent.setup();
    renderWithProviders(<WorkItemAssignmentSection workItem={{ ...item, assigneeId: "current", assigneeEmail: "current@example.com", permissions: ["workitem.self_assign"] }} project={{ ...project, projectRole: "CONTRIBUTOR", permissions: ["workitem.self_assign"] }} />);
    expect(screen.queryByLabelText("Assignee")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Atamayı kaldır" }));
    expect(unassign).toHaveBeenCalledOnce();
  });

  it("does not let a contributor manage another user but preserves self-assign", () => {
    renderWithProviders(<WorkItemAssignmentSection workItem={{ ...item, assigneeId: "target", assigneeEmail: "target@example.com", permissions: ["workitem.self_assign"] }} project={{ ...project, projectRole: "CONTRIBUTOR", permissions: ["workitem.self_assign"] }} />);
    expect(screen.queryByLabelText("Assignee")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Atamayı kaldır" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Kendime ata" })).toBeInTheDocument();
    expect(screen.getByText("target@example.com")).toBeInTheDocument();
  });

  it.each([
    { value: { ...item, permissions: [] }, archived: false },
    { value: { ...item, archivedAt: time }, archived: false },
    { value: item, archived: true },
  ])("keeps viewer and archived resources read-only", ({ value, archived }) => {
    renderWithProviders(<WorkItemAssignmentSection workItem={value} project={{ ...project, archivedAt: archived ? time : null }} />);
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
  });
});
