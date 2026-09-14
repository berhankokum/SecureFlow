import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ProjectDetails } from "@/features/projects/api/types";
import { ApiError } from "@/lib/api/apiClient";
import { renderWithProviders } from "@/test/renderWithProviders";
import type { WorkItem, Workflow } from "../api/types";
import { ProjectKanbanBoard } from "./ProjectKanbanBoard";

const list = vi.fn(), getWorkflow = vi.fn(), transition = vi.fn();
vi.mock("../api/workItemApi", () => ({
  workItemApi: {
    list: (...args: unknown[]) => list(...args),
    getWorkflow: (...args: unknown[]) => getWorkflow(...args),
    transition: (...args: unknown[]) => transition(...args),
  },
}));

const time = "2026-09-04T00:00:00Z";
const workflow: Workflow = {
  id: "workflow",
  organizationId: "org",
  name: "DEFAULT",
  states: [
    { id: "backlog", name: "BACKLOG", category: "TODO", position: 0 },
    { id: "todo", name: "TODO", category: "TODO", position: 1 },
    { id: "review", name: "IN_REVIEW", category: "IN_PROGRESS", position: 2 },
    { id: "done", name: "DONE", category: "DONE", position: 3 },
  ],
  transitions: [
    { id: "next", fromStateId: "backlog", toStateId: "todo", requiredPermission: null },
    { id: "approve", fromStateId: "review", toStateId: "done", requiredPermission: "workitem.approve" },
  ],
};
const project: ProjectDetails = {
  id: "project", organizationId: "org", key: "SF", name: "SecureFlow", description: null,
  createdBy: "owner", createdAt: time, updatedAt: time, archivedAt: null, projectRole: "MANAGER",
  permissions: ["workitem.transition", "workitem.approve"], members: [],
};

function item(stateId = "backlog", permissions: WorkItem["permissions"] = ["workitem.transition", "workitem.approve"]): WorkItem {
  const state = workflow.states.find(candidate => candidate.id === stateId)!;
  return {
    id: "item", version: 7, organizationId: "org", projectId: "project", projectKey: "SF", number: 1, key: "SF-1",
    type: "TASK", title: "Transition safely", description: null, priority: "HIGH",
    workflowStateId: state.id, workflowStateName: state.name, workflowStateCategory: state.category,
    reporterId: "owner", reporterEmail: "owner@example.com", assigneeId: null, assigneeEmail: null,
    dueDate: null, createdAt: time, updatedAt: time, archivedAt: null, permissions, labels: [],
  };
}

describe("ProjectKanbanBoard", () => {
  beforeEach(() => {
    list.mockReset().mockResolvedValue([item()]);
    getWorkflow.mockReset().mockResolvedValue(workflow);
    transition.mockReset().mockResolvedValue(undefined);
  });

  it("uses server topology for an allowed transition", async () => {
    const user = userEvent.setup();
    transition.mockImplementation(async () => { list.mockResolvedValue([item("todo")]); });
    renderWithProviders(<ProjectKanbanBoard project={project} />);

    await screen.findByText("Transition safely");
    await user.selectOptions(screen.getByLabelText("SF-1 hedef state"), "todo");

    expect(transition).toHaveBeenCalledWith("item", "todo", 7);
    expect(within(screen.getByRole("region", { name: "TODO kolonu" })).getByText("Transition safely")).toBeInTheDocument();
  });

  it("rolls optimistic movement back when backend rejects", async () => {
    const user = userEvent.setup();
    let reject!: (reason: unknown) => void;
    transition.mockReturnValue(new Promise<void>((_resolve, rejectPromise) => { reject = rejectPromise; }));
    renderWithProviders(<ProjectKanbanBoard project={project} />);
    await screen.findByText("Transition safely");

    await user.selectOptions(screen.getByLabelText("SF-1 hedef state"), "todo");
    expect(within(screen.getByRole("region", { name: "TODO kolonu" })).getByText("Transition safely")).toBeInTheDocument();
    reject(new ApiError(409, "Geçersiz workflow transition."));

    expect(await screen.findByRole("alert")).toHaveTextContent("Geçersiz workflow transition.");
    expect(within(screen.getByRole("region", { name: "BACKLOG kolonu" })).getByText("Transition safely")).toBeInTheDocument();
  });

  it("does not expose approval target without workitem.approve", async () => {
    list.mockResolvedValue([item("review", ["workitem.transition"])]);
    renderWithProviders(<ProjectKanbanBoard project={{ ...project, projectRole: "CONTRIBUTOR", permissions: ["workitem.transition"] }} />);

    await screen.findByText("Transition safely");
    expect(screen.queryByLabelText("SF-1 hedef state")).not.toBeInTheDocument();
  });
});
