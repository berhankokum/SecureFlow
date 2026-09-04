import { expect, test, type Page } from "@playwright/test";

const time = "2026-09-04T12:00:00Z";
const orgId = "11111111-1111-1111-1111-111111111111";

async function arrange(page: Page, viewer = false) {
  const permissionList = viewer ? [] : ["comment.create", "comment.update", "comment.delete"];
  const organization = { id: orgId, name: "ACME", slug: "acme", role: "MEMBER", permissions: ["member.view"], createdAt: time, archivedAt: null };
  const project = { id: "project", organizationId: orgId, key: "SF", name: "SecureFlow", description: null, createdBy: "owner", createdAt: time, updatedAt: time, archivedAt: null, projectRole: viewer ? "VIEWER" : "MANAGER", permissions: permissionList, members: [] };
  const item = { id: "item", organizationId: orgId, projectId: "project", projectKey: "SF", number: 1, key: "SF-1", type: "TASK", title: "Collaboration", description: null, priority: "MEDIUM", workflowStateId: "backlog", workflowStateName: "BACKLOG", workflowStateCategory: "TODO", reporterId: "owner", reporterEmail: "owner@example.com", assigneeId: null, assigneeEmail: null, dueDate: null, createdAt: time, updatedAt: time, archivedAt: null, permissions: permissionList, labels: [] };
  let comments = viewer ? [{ id: "comment", organizationId: orgId, workItemId: "item", authorId: "owner", authorEmail: "owner@example.com", content: "Salt okunur yorum", createdAt: time, updatedAt: time, deletedAt: null, canUpdate: false, canDelete: false }] : [];
  let activity = [{ id: "created", organizationId: orgId, projectId: "project", workItemId: "item", actorId: "owner", actorEmail: "owner@example.com", type: "WORK_ITEM_CREATED", metadata: { workItemKey: "SF-1" }, createdAt: time }];
  const mutations: { method: string; csrf?: string }[] = [];

  await page.route("**/api/v1/**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
    if (path.endsWith("/antiforgery/token")) return route.fulfill({ status: 204, headers: { "set-cookie": "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict" } });
    if (method !== "GET") mutations.push({ method, csrf: request.headers()["x-csrf-token"] });
    if (path === "/api/v1/me") return json({ id: "owner", email: "owner@example.com", emailConfirmed: true });
    if (path === "/api/v1/organizations") return json([organization]);
    if (path === `/api/v1/organizations/${orgId}/projects`) return json([project]);
    if (path === `/api/v1/organizations/${orgId}/members`) return json([]);
    if (path === "/api/v1/projects/project") return json(project);
    if (path === "/api/v1/projects/project/work-items") return json([item]);
    if (path === "/api/v1/projects/project/labels") return json([]);
    if (path === "/api/v1/work-items/item") return json(item);
    if (path === "/api/v1/work-items/item/comments") {
      if (method === "GET") return json(comments);
      const comment = { id: "comment", organizationId: orgId, workItemId: "item", authorId: "owner", authorEmail: "owner@example.com", content: request.postDataJSON().content, createdAt: time, updatedAt: time, deletedAt: null, canUpdate: true, canDelete: true };
      comments = [comment];
      activity = [{ id: "comment-created", organizationId: orgId, projectId: "project", workItemId: "item", actorId: "owner", actorEmail: "owner@example.com", type: "COMMENT_CREATED", metadata: { commentId: "comment" }, createdAt: time }, ...activity];
      return json(comment, 201);
    }
    if (path === "/api/v1/work-items/item/activity") return json(activity);
    if (path === "/api/v1/comments/comment" && method === "PATCH") {
      comments = comments.map(comment => ({ ...comment, content: request.postDataJSON().content, updatedAt: "2026-09-04T12:05:00Z" }));
      return route.fulfill({ status: 204 });
    }
    if (path === "/api/v1/comments/comment" && method === "DELETE") {
      comments = comments.map(comment => ({ ...comment, content: null, deletedAt: "2026-09-04T12:06:00Z", canUpdate: false, canDelete: false }));
      return route.fulfill({ status: 204 });
    }
    return json({ title: "Unexpected test request" }, 404);
  });

  return mutations;
}

test("comment editor, edit, soft delete and activity timeline work end to end", async ({ page }) => {
  const mutations = await arrange(page);
  await page.goto("/app/acme/work-items/SF-1");
  await page.getByRole("textbox", { name: "Yorum", exact: true }).fill("Yetki kontrol edildi");
  await page.getByRole("button", { name: "Yorum ekle" }).click();
  await expect(page.getByText("Yetki kontrol edildi")).toBeVisible();
  await expect(page.getByText(/yorum ekledi/)).toBeVisible();

  const row = page.getByRole("region", { name: "Yorumlar" }).getByRole("listitem");
  await row.getByRole("button", { name: "Düzenle" }).click();
  await row.getByRole("textbox", { name: "Yorum", exact: true }).fill("Güncellendi");
  await row.getByRole("button", { name: "Değişiklikleri kaydet" }).click();
  await expect(page.getByText("Güncellendi")).toBeVisible();
  page.once("dialog", dialog => dialog.accept());
  await page.getByRole("button", { name: "Sil" }).click();
  await expect(page.getByText("Bu yorum silindi.")).toBeVisible();
  expect(mutations.map(item => item.method)).toEqual(["POST", "PATCH", "DELETE"]);
  expect(mutations.every(item => item.csrf === "e2e-csrf-token")).toBe(true);
});

test("viewer sees comments and activity without mutation controls", async ({ page }) => {
  await arrange(page, true);
  await page.goto("/app/acme/work-items/SF-1");
  await expect(page.getByText("Salt okunur yorum")).toBeVisible();
  await expect(page.getByText(/SF-1 oluşturuldu/)).toBeVisible();
  await expect(page.getByRole("button", { name: "Yorum ekle" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Düzenle" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Sil" })).toHaveCount(0);
});
