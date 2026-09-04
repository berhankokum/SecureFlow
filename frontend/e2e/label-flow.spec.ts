import { expect, test, type Page } from "@playwright/test";

const timestamp = "2026-09-03T00:00:00Z";
const orgId = "11111111-1111-1111-1111-111111111111";
type Label = { id: string; name: string; organizationId: string; projectId: string; createdBy: string; createdAt: string; updatedAt: string };

// Browser/UI contract suite; real authorization, persistence and concurrency run in PostgreSQL integration tests.
async function arrange(page: Page, role: "MANAGER" | "CONTRIBUTOR" | "VIEWER" = "MANAGER", archived = false) {
  const permissions = role === "MANAGER" ? ["label.create", "label.update", "label.delete", "label.assign"] : role === "CONTRIBUTOR" ? ["label.assign"] : [];
  const organization = { id: orgId, name: "ACME", slug: "acme", role: "MEMBER", permissions: ["member.view"], createdAt: timestamp, archivedAt: null };
  const project = { id: "project-1", organizationId: orgId, key: "SF", name: "SecureFlow", description: null, createdBy: "owner", createdAt: timestamp, updatedAt: timestamp, archivedAt: archived ? timestamp : null, permissions, projectRole: role };
  let labels: Label[] = [{ id: "label-1", name: "Security", organizationId: orgId, projectId: project.id, createdBy: "owner", createdAt: timestamp, updatedAt: timestamp }];
  let assigned: string[] = [];
  const workItem = () => ({ id: "item-1", organizationId: orgId, projectId: project.id, projectKey: "SF", number: 1, key: "SF-1", type: "TASK", title: "Secure delivery", description: null, priority: "HIGH", workflowStateId: "state-backlog", workflowStateName: "BACKLOG", workflowStateCategory: "TODO", reporterId: "owner", reporterEmail: "owner@example.com", assigneeId: null, assigneeEmail: null, dueDate: null, createdAt: timestamp, updatedAt: timestamp, archivedAt: null, permissions, labels: labels.filter(label => assigned.includes(label.id)).map(label => ({ id: label.id, name: label.name, assignedBy: "owner", assignedAt: timestamp })) });
  const mutations: { method: string; csrf: string | undefined }[] = [];
  await page.route("**/api/v1/**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
    if (path.endsWith("/antiforgery/token")) return route.fulfill({ status: 204, headers: { "set-cookie": "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict" } });
    if (method !== "GET") mutations.push({ method, csrf: request.headers()["x-csrf-token"] });
    if (path === "/api/v1/me") return json({ id: "owner", email: "owner@example.com", emailConfirmed: true });
    if (path === "/api/v1/organizations") return json([organization]);
    if (path.endsWith(`/organizations/${orgId}/projects`)) return json([project]);
    if (path.endsWith(`/organizations/${orgId}/members`)) return json([]);
    if (path === "/api/v1/projects/project-1") return json({ ...project, members: [] });
    if (path === "/api/v1/projects/project-1/work-items") return json([workItem()]);
    if (path === "/api/v1/work-items/item-1") return json(workItem());
    if (path === "/api/v1/projects/project-1/labels") {
      if (method === "GET") return json(labels);
      const name = request.postDataJSON().name as string;
      if (labels.some(label => label.name.toUpperCase() === name.toUpperCase())) return json({ title: "Bu Project içinde aynı isimli Label zaten var." }, 409);
      const label = { ...labels[0], id: "label-2", name };
      labels = [...labels, label];
      return json(label, 201);
    }
    if (path.startsWith("/api/v1/work-items/item-1/labels/")) {
      const id = path.split("/").at(-1)!;
      assigned = method === "POST" ? [...new Set([...assigned, id])] : assigned.filter(value => value !== id);
      return route.fulfill({ status: 204 });
    }
    if (path.startsWith("/api/v1/labels/")) {
      const id = path.split("/").at(-1)!;
      if (method === "PATCH") labels = labels.map(label => label.id === id ? { ...label, name: request.postDataJSON().name } : label);
      if (method === "DELETE") { labels = labels.filter(label => label.id !== id); assigned = assigned.filter(value => value !== id); }
      return route.fulfill({ status: 204 });
    }
    return json({ title: "Unexpected test request" }, 404);
  });
  return mutations;
}

test("manager creates, assigns, removes, renames and deletes labels with cache refresh", async ({ page }) => {
  const mutations = await arrange(page);
  await page.goto("/app/acme/projects/SF");
  const management = page.getByRole("region", { name: "Labels", exact: true });
  await management.getByRole("button", { name: "Label oluştur" }).click();
  await management.getByLabel("Label adı").fill("Security");
  await management.getByRole("button", { name: "Label kaydet" }).click();
  await expect(management.getByRole("alert")).toContainText("aynı isimli");
  await management.getByLabel("Label adı").fill("Urgent");
  await management.getByRole("button", { name: "Label kaydet" }).click();
  await expect(management.getByText("Urgent", { exact: true })).toBeVisible();
  await page.getByRole("link", { name: /SF-1.*Secure delivery/s }).click();
  const itemLabels = page.getByRole("region", { name: "WorkItem Labels" });
  await itemLabels.getByLabel("Atanacak Label").selectOption("label-2");
  await itemLabels.getByRole("button", { name: "Label ata" }).click();
  await expect(itemLabels.getByRole("button", { name: "Urgent Label kaldır" })).toBeVisible();
  await itemLabels.getByRole("button", { name: "Urgent Label kaldır" }).click();
  await expect(itemLabels.getByText("Atanmış Label yok.")).toBeVisible();
  await itemLabels.getByLabel("Atanacak Label").selectOption("label-2");
  await itemLabels.getByRole("button", { name: "Label ata" }).click();
  await expect(itemLabels.getByRole("button", { name: "Urgent Label kaldır" })).toBeVisible();
  await page.getByRole("link", { name: "← SF Project" }).click();
  await management.getByRole("button", { name: "Urgent Label düzenle" }).click();
  await management.getByLabel("Label adı").fill("Priority");
  await management.getByRole("button", { name: "Label kaydet" }).click();
  await expect(page.getByRole("link", { name: /SF-1.*Priority/s })).toBeVisible();
  page.once("dialog", dialog => dialog.accept());
  await management.getByRole("button", { name: "Priority Label sil" }).click();
  await expect(management.getByText("Priority", { exact: true })).not.toBeVisible();
  await page.getByRole("link", { name: /SF-1.*Secure delivery/s }).click();
  await expect(itemLabels.getByText("Atanmış Label yok.")).toBeVisible();
  expect(mutations.length).toBeGreaterThanOrEqual(7);
  expect(mutations.every(mutation => mutation.csrf === "e2e-csrf-token")).toBe(true);
});

test("contributor can assign labels but cannot manage them", async ({ page }) => {
  await arrange(page, "CONTRIBUTOR");
  await page.goto("/app/acme/projects/SF");
  await expect(page.getByRole("region", { name: "Labels", exact: true }).getByRole("button")).toHaveCount(0);
  await page.getByRole("link", { name: /SF-1.*Secure delivery/s }).click();
  await page.getByLabel("Atanacak Label").selectOption("label-1");
  await page.getByRole("button", { name: "Label ata" }).click();
  await expect(page.getByRole("button", { name: "Security Label kaldır" })).toBeVisible();
});

for (const { role, archived } of [{ role: "VIEWER", archived: false }, { role: "MANAGER", archived: true }] as const) {
  test(`${role} archived=${archived} sees read-only labels`, async ({ page }) => {
    await arrange(page, role, archived);
    await page.goto("/app/acme/projects/SF");
    await expect(page.getByRole("region", { name: "Labels", exact: true }).getByRole("button")).toHaveCount(0);
    await page.getByRole("link", { name: /SF-1.*Secure delivery/s }).click();
    await expect(page.getByRole("heading", { name: "WorkItem Labels" })).toBeVisible();
    await expect(page.getByLabel("Atanacak Label")).toHaveCount(0);
  });
}
