import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { WorkItem } from "@/features/work-items/api/types";
import { renderWithProviders } from "@/test/renderWithProviders";
import type { Attachment } from "../api/types";
import { WorkItemAttachmentsSection } from "./WorkItemAttachmentsSection";

const { upload, remove, download } = vi.hoisted(() => ({
  upload: vi.fn(),
  remove: vi.fn(),
  download: vi.fn(),
}));
let attachmentData: Attachment[] = [];

vi.mock("../hooks/useAttachments", () => ({
  useAttachments: () => ({ data: attachmentData, isLoading: false, isError: false }),
  useUploadAttachment: () => ({ mutateAsync: upload, isPending: false }),
  useDeleteAttachment: () => ({ mutateAsync: remove, isPending: false }),
}));

vi.mock("../api/attachmentApi", () => ({
  attachmentApi: { download },
}));

const time = "2026-09-05T12:00:00Z";
const workItem: WorkItem = {
  id: "workitem", organizationId: "org", projectId: "project", projectKey: "SF", number: 1, key: "SF-1",
  type: "TASK", title: "Files", description: null, priority: "MEDIUM", workflowStateId: "backlog",
  workflowStateName: "BACKLOG", workflowStateCategory: "TODO", reporterId: "owner", reporterEmail: "owner@example.com",
  assigneeId: null, assigneeEmail: null, dueDate: null, createdAt: time, updatedAt: time, archivedAt: null,
  permissions: ["attachment.upload", "attachment.delete"], labels: [],
};

describe("WorkItemAttachmentsSection", () => {
  beforeEach(() => {
    attachmentData = [];
    upload.mockReset().mockImplementation(async ({ onProgress }) => { onProgress(50); });
    remove.mockReset().mockResolvedValue(undefined);
    download.mockReset().mockResolvedValue(undefined);
  });

  it("uploads an allowed file and reports client-side validation errors", async () => {
    const user = userEvent.setup();
    renderWithProviders(<WorkItemAttachmentsSection workItem={workItem} projectArchived={false} />);

    const input = screen.getByLabelText("Dosya seç");
    const pdf = new File(["%PDF-1.7"], "evidence.pdf", { type: "application/pdf" });
    await user.upload(input, pdf);
    expect(upload).toHaveBeenCalledWith(expect.objectContaining({ file: pdf, onProgress: expect.any(Function) }));

    fireEvent.change(input, {
      target: { files: [new File(["bad"], "payload.exe", { type: "application/octet-stream" })] },
    });
    expect(await screen.findByRole("alert")).toHaveTextContent("Yalnız PDF, PNG ve JPEG");
  });

  it("renders server-derived delete capability and downloads through the API", async () => {
    const user = userEvent.setup();
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
    attachmentData = [{
      id: "attachment", organizationId: "org", workItemId: "workitem", originalFileName: "evidence.pdf",
      contentType: "application/pdf", size: 2048, uploadedBy: "owner", uploadedByEmail: "owner@example.com",
      uploadedAt: time, canDelete: true,
    }];
    renderWithProviders(<WorkItemAttachmentsSection workItem={workItem} projectArchived={false} />);

    const row = screen.getByText("evidence.pdf").closest("li")!;
    await user.click(within(row).getByRole("button", { name: "İndir" }));
    expect(download).toHaveBeenCalledWith(attachmentData[0]);
    await user.click(within(row).getByRole("button", { name: "Sil" }));
    expect(remove).toHaveBeenCalledWith("attachment");
    confirm.mockRestore();
  });

  it("keeps archived resources read-only while preserving the list", () => {
    attachmentData = [{
      id: "attachment", organizationId: "org", workItemId: "workitem", originalFileName: "kept.png",
      contentType: "image/png", size: 12, uploadedBy: "owner", uploadedByEmail: "owner@example.com",
      uploadedAt: time, canDelete: false,
    }];
    renderWithProviders(<WorkItemAttachmentsSection workItem={{ ...workItem, archivedAt: time }} projectArchived={false} />);

    expect(screen.getByText("kept.png")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Dosya seç" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Sil" })).not.toBeInTheDocument();
  });
});
