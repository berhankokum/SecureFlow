import { expect, test, type Page } from "@playwright/test";

const time = "2026-09-05T12:00:00Z";
const orgId = "11111111-1111-1111-1111-111111111111";

async function arrange(page: Page, viewer = false) {
  const permissions = viewer ? [] : ["attachment.upload", "attachment.delete"];
  const organization = { id: orgId, name: "ACME", slug: "acme", role: "MEMBER", permissions: ["member.view"], createdAt: time, archivedAt: null };
  const project = { id: "project", organizationId: orgId, key: "SF", name: "SecureFlow", description: null, createdBy: "owner", createdAt: time, updatedAt: time, archivedAt: null, projectRole: viewer ? "VIEWER" : "MANAGER", permissions, members: [] };
  const item = { id: "item", organizationId: orgId, projectId: "project", projectKey: "SF", number: 1, key: "SF-1", type: "TASK", title: "Attachments", description: null, priority: "MEDIUM", workflowStateId: "backlog", workflowStateName: "BACKLOG", workflowStateCategory: "TODO", reporterId: "owner", reporterEmail: "owner@example.com", assigneeId: null, assigneeEmail: null, dueDate: null, createdAt: time, updatedAt: time, archivedAt: null, permissions, labels: [] };
  let attachments = viewer ? [{ id: "existing", organizationId: orgId, workItemId: "item", originalFileName: "readonly.pdf", contentType: "application/pdf", size: 12, uploadedBy: "owner", uploadedByEmail: "owner@example.com", uploadedAt: time, canDelete: false }] : [];
  let activity: object[] = [];
  const mutationHeaders: Array<string | undefined> = [];

  await page.route("**/api/v1/**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
    if (path.endsWith("/antiforgery/token")) return route.fulfill({ status: 204, headers: { "set-cookie": "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict" } });
    if (method !== "GET") mutationHeaders.push(request.headers()["x-csrf-token"]);
    if (path === "/api/v1/me") return json({ id: "owner", email: "owner@example.com", emailConfirmed: true });
    if (path === "/api/v1/organizations") return json([organization]);
    if (path === `/api/v1/organizations/${orgId}/projects`) return json([project]);
    if (path === `/api/v1/organizations/${orgId}/members`) return json([]);
    if (path === "/api/v1/projects/project") return json(project);
    if (path === "/api/v1/projects/project/work-items") return json([item]);
    if (path === "/api/v1/projects/project/labels") return json([]);
    if (path === "/api/v1/work-items/item") return json(item);
    if (path === "/api/v1/work-items/item/comments") return json([]);
    if (path === "/api/v1/work-items/item/activity") return json(activity);
    if (path === "/api/v1/work-items/item/attachments") {
      if (method === "GET") return json(attachments);
      const attachment = { id: "uploaded", organizationId: orgId, workItemId: "item", originalFileName: "evidence.pdf", contentType: "application/pdf", size: 18, uploadedBy: "owner", uploadedByEmail: "owner@example.com", uploadedAt: time, canDelete: true };
      attachments = [attachment];
      activity = [{ id: "activity", organizationId: orgId, projectId: "project", workItemId: "item", actorId: "owner", actorEmail: "owner@example.com", type: "ATTACHMENT_UPLOADED", metadata: { attachmentId: "uploaded", fileName: "evidence.pdf" }, createdAt: time }];
      return json(attachment, 201);
    }
    if (path === "/api/v1/attachments/uploaded" && method === "GET") return route.fulfill({ status: 200, contentType: "application/pdf", body: "%PDF-1.7 evidence" });
    if (path === "/api/v1/attachments/uploaded" && method === "DELETE") {
      attachments = [];
      return route.fulfill({ status: 204 });
    }
    return json({ title: "Unexpected test request" }, 404);
  });

  return mutationHeaders;
}

test("file picker uploads, lists, downloads and deletes an attachment", async ({ page }) => {
  const mutationHeaders = await arrange(page);
  await page.goto("/app/acme/work-items/SF-1");
  await page.getByLabel("Dosya seç").setInputFiles({
    name: "evidence.pdf",
    mimeType: "application/pdf",
    buffer: Buffer.from("%PDF-1.7 evidence"),
  });
  await expect(page.getByText("evidence.pdf", { exact: true })).toBeVisible();
  await expect(page.getByText(/evidence.pdf attachment'ını yükledi/)).toBeVisible();

  const downloadPromise = page.waitForEvent("download");
  await page.getByRole("button", { name: "İndir" }).click();
  expect((await downloadPromise).suggestedFilename()).toBe("evidence.pdf");

  page.once("dialog", dialog => dialog.accept());
  await page.getByRole("button", { name: "Sil" }).click();
  await expect(page.getByText("Henüz attachment yok.")).toBeVisible();
  expect(mutationHeaders).toEqual(["e2e-csrf-token", "e2e-csrf-token"]);
});

test("viewer sees the attachment without upload or delete controls", async ({ page }) => {
  await arrange(page, true);
  await page.goto("/app/acme/work-items/SF-1");
  await expect(page.getByText("readonly.pdf")).toBeVisible();
  await expect(page.getByRole("button", { name: "Dosya seç" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Sil" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "İndir" })).toBeVisible();
});
