import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/renderWithProviders";
import { ApiError } from "@/lib/api/apiClient";
import type { ProjectDetails } from "@/features/projects/api/types";
import type { WorkItem } from "@/features/work-items/api/types";
import { ProjectLabelsSection } from "./ProjectLabelsSection";
import { WorkItemLabelsSection } from "./WorkItemLabelsSection";
import { labelSchema } from "../schemas/labelSchema";

const create = vi.fn(), update = vi.fn(), remove = vi.fn(), assign = vi.fn();
const label = { id: "label-1", name: "Security", organizationId: "org", projectId: "project", createdBy: "owner", createdAt: "2026-09-03T00:00:00Z", updatedAt: "2026-09-03T00:00:00Z" };
const project: ProjectDetails = {
  id: "project", organizationId: "org", key: "SF", name: "SecureFlow", description: null,
  createdBy: "owner", createdAt: label.createdAt, updatedAt: label.createdAt, archivedAt: null,
  projectRole: "MANAGER", permissions: ["label.create", "label.update", "label.delete", "label.assign"], members: [],
};
const workItem: WorkItem = {
  id: "workitem", organizationId: "org", projectId: "project", projectKey: "SF", number: 1, key: "SF-1",
  type: "TASK", title: "Test", description: null, priority: "LOW", reporterId: "owner", reporterEmail: "owner@example.com",
  assigneeId: null, assigneeEmail: null, dueDate: null, createdAt: label.createdAt, updatedAt: label.createdAt, archivedAt: null, permissions: ["label.assign"], labels: [],
};

vi.mock("../hooks/useLabels", () => ({
  useLabels: () => ({ data: [label], isLoading: false, isError: false }),
  useCreateLabel: () => ({ mutateAsync: create }),
  useUpdateLabel: () => ({ mutateAsync: update }),
  useDeleteLabel: () => ({ mutateAsync: remove, isPending: false }),
  useSetWorkItemLabel: () => ({ mutateAsync: assign, isPending: false }),
}));

describe("Labels vertical slice UI", () => {
  beforeEach(() => { for (const fn of [create, update, remove, assign]) fn.mockReset().mockResolvedValue(undefined); });

  it("creates normalized labels and reports duplicate conflicts", async () => {
    const user = userEvent.setup();
    create.mockRejectedValueOnce(new ApiError(409, "Bu isim zaten var."));
    renderWithProviders(<ProjectLabelsSection project={project} />);
    await user.click(screen.getByRole("button", { name: "Label oluştur" }));
    await user.type(screen.getByLabelText("Label adı"), "  Ｓｅｃｕｒｉｔｙ  ");
    await user.click(screen.getByRole("button", { name: "Label kaydet" }));
    expect(create).toHaveBeenCalledWith("Security");
    expect(await screen.findByRole("alert")).toHaveTextContent("Bu isim zaten var.");
    await user.click(screen.getByRole("button", { name: "Label kaydet" }));
    expect(screen.queryByLabelText("Label adı")).not.toBeInTheDocument();
  });

  it("renames and deletes with confirmation", async () => {
    const user = userEvent.setup();
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
    renderWithProviders(<ProjectLabelsSection project={project} />);
    await user.click(screen.getByRole("button", { name: "Security Label düzenle" }));
    await user.clear(screen.getByLabelText("Label adı"));
    await user.type(screen.getByLabelText("Label adı"), "Urgent");
    await user.click(screen.getByRole("button", { name: "Label kaydet" }));
    expect(update).toHaveBeenCalledWith({ id: "label-1", name: "Urgent" });
    await user.click(screen.getByRole("button", { name: "Security Label sil" }));
    expect(remove).toHaveBeenCalledWith("label-1");
    confirm.mockRestore();
  });

  it("validates empty and overlong normalized names", () => {
    expect(labelSchema.safeParse({ name: " " }).success).toBe(false);
    expect(labelSchema.safeParse({ name: "ﬃ".repeat(17) }).success).toBe(false);
  });

  it.each(["CONTRIBUTOR", "VIEWER"])("hides CRUD for %s", role => {
    renderWithProviders(<ProjectLabelsSection project={{ ...project, projectRole: role as "CONTRIBUTOR" | "VIEWER", permissions: role === "CONTRIBUTOR" ? ["label.assign"] : [] }} />);
    expect(screen.getByText("Security")).toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("allows contributor to assign an existing project label", async () => {
    const user = userEvent.setup();
    renderWithProviders(<WorkItemLabelsSection workItem={workItem} projectArchived={false} />);
    await user.selectOptions(screen.getByLabelText("Atanacak Label"), "label-1");
    await user.click(screen.getByRole("button", { name: "Label ata" }));
    expect(assign).toHaveBeenCalledWith({ labelId: "label-1", assigned: true });
  });

  it("removes an assigned label and excludes it from selector", async () => {
    const user = userEvent.setup();
    renderWithProviders(<WorkItemLabelsSection workItem={{ ...workItem, labels: [{ id: label.id, name: label.name, assignedBy: "owner", assignedAt: label.createdAt }] }} projectArchived={false} />);
    expect(screen.queryByRole("option", { name: "Security" })).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Security Label kaldır" }));
    expect(assign).toHaveBeenCalledWith({ labelId: "label-1", assigned: false });
  });

  it.each([
    { item: { ...workItem, permissions: [] }, archived: false },
    { item: { ...workItem, archivedAt: label.createdAt }, archived: false },
    { item: workItem, archived: true },
  ])("makes viewer and archived resources read-only", ({ item, archived }) => {
    renderWithProviders(<WorkItemLabelsSection workItem={item} projectArchived={archived} />);
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("hides all management actions for archived projects", () => {
    renderWithProviders(<ProjectLabelsSection project={{ ...project, archivedAt: label.createdAt }} />);
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});
