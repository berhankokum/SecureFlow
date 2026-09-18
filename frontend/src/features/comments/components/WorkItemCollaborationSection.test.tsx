import { beforeEach, describe, expect, it, vi } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithProviders } from "@/test/renderWithProviders";
import type { WorkItem } from "@/features/work-items/api/types";
import type { ActivityEvent, Comment } from "../api/types";
import { WorkItemCollaborationSection } from "./WorkItemCollaborationSection";

const create = vi.fn(), update = vi.fn(), remove = vi.fn();
let commentData: Comment[] = [];
let activityData: ActivityEvent[] = [];

vi.mock("../hooks/useCollaboration", () => ({
  useComments: () => ({ data: commentData, isLoading: false, isError: false }),
  useActivity: () => ({ data: activityData, isLoading: false, isError: false }),
  useCreateComment: () => ({ mutateAsync: create, isPending: false }),
  useUpdateComment: () => ({ mutateAsync: update, isPending: false }),
  useDeleteComment: () => ({ mutateAsync: remove, isPending: false }),
}));

const time = "2026-09-04T12:00:00Z";
const workItem: WorkItem = {
  id: "workitem", organizationId: "org", projectId: "project", projectKey: "SF", number: 1, key: "SF-1",
  type: "TASK", title: "Comments", description: null, priority: "MEDIUM", workflowStateId: "backlog",
  workflowStateName: "BACKLOG", workflowStateCategory: "TODO", reporterId: "owner", reporterEmail: "owner@example.com",
  assigneeId: null, assigneeEmail: null, dueDate: null, createdAt: time, updatedAt: time, archivedAt: null,
  permissions: ["comment.create", "comment.update", "comment.delete"], labels: [],
};

describe("WorkItemCollaborationSection", () => {
  it("renders hostile stored comment content as text, never executable HTML", () => {
    const payload = '<img src=x onerror="alert(document.cookie)"><script>alert(1)</script>';
    commentData = [{ id: "xss", organizationId: "org", workItemId: "workitem", authorId: "author",
      authorEmail: "author@example.com", content: payload, createdAt: time, updatedAt: time,
      deletedAt: null, canUpdate: false, canDelete: false, mentions: [] }];
    const { container } = renderWithProviders(<WorkItemCollaborationSection workItem={workItem} projectArchived={false} />);
    expect(screen.getByText(payload)).toBeInTheDocument();
    expect(container.querySelector("script, img, iframe")).toBeNull();
  });
  beforeEach(() => {
    commentData = [];
    activityData = [];
    for (const mock of [create, update, remove]) mock.mockReset().mockResolvedValue(undefined);
  });

  it("validates and creates a comment", async () => {
    const user = userEvent.setup();
    renderWithProviders(<WorkItemCollaborationSection workItem={workItem} projectArchived={false} />);

    await user.click(screen.getByRole("button", { name: "Yorum ekle" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Yorum boş olamaz");
    await user.type(screen.getByLabelText("Yorum"), "  Yetki kontrol edildi.  ");
    await user.click(screen.getByRole("button", { name: "Yorum ekle" }));
    expect(create).toHaveBeenCalledWith({ content: "Yetki kontrol edildi.", mentionedUserIds: [] });
  });

  it("renders deleted comments as a placeholder without leaking content", () => {
    commentData = [{
      id: "comment", organizationId: "org", workItemId: "workitem", authorId: "author",
      authorEmail: "author@example.com", content: null, createdAt: time, updatedAt: time,
      deletedAt: time, canUpdate: false, canDelete: false,
      mentions: [],
    }];
    renderWithProviders(<WorkItemCollaborationSection workItem={workItem} projectArchived={false} />);

    expect(screen.getByText("Bu yorum silindi.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Düzenle" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Sil" })).not.toBeInTheDocument();
  });

  it("edits and deletes only when server-derived capabilities allow it", async () => {
    const user = userEvent.setup();
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
    commentData = [{
      id: "comment", organizationId: "org", workItemId: "workitem", authorId: "author",
      authorEmail: "author@example.com", content: "İlk", createdAt: time, updatedAt: time,
      deletedAt: null, canUpdate: true, canDelete: true,
      mentions: [],
    }];
    renderWithProviders(<WorkItemCollaborationSection workItem={workItem} projectArchived={false} />);

    const row = screen.getByText("İlk").closest("li")!;
    await user.click(within(row).getByRole("button", { name: "Düzenle" }));
    const editor = within(row).getByLabelText("Yorum");
    await user.clear(editor);
    await user.type(editor, "İkinci");
    await user.click(within(row).getByRole("button", { name: "Değişiklikleri kaydet" }));
    expect(update).toHaveBeenCalledWith({ commentId: "comment", content: "İkinci", mentionedUserIds: [] });
    await user.click(within(row).getByRole("button", { name: "Sil" }));
    expect(remove).toHaveBeenCalledWith("comment");
    confirm.mockRestore();
  });

  it("inserts a project-access candidate as an email mention and sends the matching user id", async () => {
    const user = userEvent.setup();
    renderWithProviders(<WorkItemCollaborationSection workItem={workItem} projectArchived={false} mentionCandidates={[{ userId: "target", email: "target@example.com" }]} />);

    await user.selectOptions(screen.getByLabelText("Mention eklenecek kullanıcı"), "target");
    await user.click(screen.getByRole("button", { name: "Mention ekle" }));
    expect(screen.getByLabelText("Yorum")).toHaveValue("@target@example.com ");
    await user.type(screen.getByLabelText("Yorum"), "kontrol eder misin?");
    await user.click(screen.getByRole("button", { name: "Yorum ekle" }));

    expect(create).toHaveBeenCalledWith({
      content: "@target@example.com kontrol eder misin?",
      mentionedUserIds: ["target"],
    });
  });

  it("shows a readable activity timeline and keeps archived resources read-only", () => {
    activityData = [{
      id: "activity", organizationId: "org", projectId: "project", workItemId: "workitem",
      actorId: "actor", actorEmail: "actor@example.com", type: "WORK_ITEM_TRANSITIONED",
      metadata: { fromState: "TODO", toState: "IN_PROGRESS" }, createdAt: time,
    }];
    renderWithProviders(<WorkItemCollaborationSection workItem={{ ...workItem, archivedAt: time }} projectArchived={false} />);

    expect(screen.getByText(/TODO → IN_PROGRESS geçişi yapıldı/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Yorum ekle" })).not.toBeInTheDocument();
  });
});
