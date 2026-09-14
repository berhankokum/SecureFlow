import { expect, test, type Page } from "@playwright/test";

const time = "2026-09-04T00:00:00Z";
const orgId = "11111111-1111-1111-1111-111111111111";

async function arrange(page: Page, role: "MANAGER" | "CONTRIBUTOR" | "VIEWER", archived = false) {
  const actorPermissions = role === "MANAGER"
    ? ["workitem.assign", "workitem.self_assign"]
    : role === "CONTRIBUTOR" ? ["workitem.self_assign"] : [];
  const organization = { id: orgId, name: "ACME", slug: "acme", role: "MEMBER", permissions: ["member.view"], createdAt: time, archivedAt: null };
  const project = {
    id: "project-1", organizationId: orgId, key: "SF", name: "SecureFlow", description: null,
    createdBy: "owner", createdAt: time, updatedAt: time, archivedAt: archived ? time : null,
    projectRole: role, permissions: actorPermissions,
    members: [
      { userId: "actor", email: "actor@example.com", role },
      { userId: "target", email: "target@example.com", role: "CONTRIBUTOR" },
    ],
  };
  let assigneeId: string | null = null;
  let assigneeEmail: string | null = null;
  const workItem = () => ({
    id: "item-1", organizationId: orgId, projectId: project.id, projectKey: "SF", number: 1, key: "SF-1",
    type: "TASK", title: "Assignment flow", description: null, priority: "MEDIUM", workflowStateId: "state-backlog",
    workflowStateName: "BACKLOG", workflowStateCategory: "TODO", reporterId: "actor",
    reporterEmail: "actor@example.com", assigneeId, assigneeEmail, dueDate: null,
    createdAt: time, updatedAt: time, archivedAt: null, permissions: actorPermissions, labels: [],
  });
  const mutations: { path: string; csrf?: string }[] = [];
  await page.route("**/api/v1/**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
    if (path.endsWith("/antiforgery/token")) return route.fulfill({ status: 204, headers: { "set-cookie": "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict" } });
    if (method !== "GET") mutations.push({ path, csrf: request.headers()["x-csrf-token"] });
    if (path === "/api/v1/me") return json({ id: "actor", email: "actor@example.com", emailConfirmed: true });
    if (path === "/api/v1/organizations") return json([organization]);
    if (path === `/api/v1/organizations/${orgId}/projects`) return json([{ ...project, members: undefined }]);
    if (path === `/api/v1/organizations/${orgId}/members`) return json([]);
    if (path === "/api/v1/projects/project-1") return json(project);
    if (path === "/api/v1/projects/project-1/work-items") {
      const assignee = new URL(request.url()).searchParams.get("assignee");
      return json(assignee && assignee !== assigneeId ? [] : [workItem()]);
    }
    if (path === "/api/v1/projects/project-1/labels") return json([]);
    if (path === "/api/v1/work-items/item-1" && method === "GET") return json(workItem());
    if (path === "/api/v1/work-items/item-1/assign" && method === "POST") {
      assigneeId = request.postDataJSON().assigneeId;
      assigneeEmail = assigneeId === "target" ? "target@example.com" : "actor@example.com";
      return route.fulfill({ status: 204 });
    }
    if (path === "/api/v1/work-items/item-1/self-assign" && method === "POST") {
      assigneeId = "actor"; assigneeEmail = "actor@example.com";
      return route.fulfill({ status: 204 });
    }
    if (path === "/api/v1/work-items/item-1/assignment" && method === "DELETE") {
      assigneeId = null; assigneeEmail = null;
      return route.fulfill({ status: 204 });
    }
    return json({ title: "Unexpected test request" }, 404);
  });
  return { mutations, setAssignee(id: string | null, email: string | null) { assigneeId = id; assigneeEmail = email; } };
}

test("manager assigns, reassigns, self-assigns and unassigns", async ({ page }) => {
  const state = await arrange(page, "MANAGER");
  await page.goto("/app/acme/projects/SF");
  await expect(page.getByText("Unassigned", { exact: true })).toBeVisible();
  await page.getByRole("link", { name: /SF-1.*Assignment flow/s }).click();
  const assignment = page.getByRole("region", { name: "Assignment" });
  await assignment.getByLabel("Assignee").selectOption("target");
  await assignment.getByRole("button", { name: "Ata", exact: true }).click();
  await expect(assignment.locator("p").filter({ hasText: "Assignee:" })).toContainText("target@example.com");
  await assignment.getByLabel("Assignee").selectOption("actor");
  await assignment.getByRole("button", { name: "Yeniden ata" }).click();
  await expect(assignment.locator("p").filter({ hasText: "Assignee:" })).toContainText("actor@example.com");
  await assignment.getByRole("button", { name: "Atamayı kaldır" }).click();
  await expect(assignment.getByText("Unassigned", { exact: true })).toBeVisible();
  await assignment.getByRole("button", { name: "Kendime ata" }).click();
  await expect(assignment.locator("p").filter({ hasText: "Assignee:" })).toContainText("actor@example.com");
  expect(state.mutations).toHaveLength(4);
  expect(state.mutations.every(mutation => mutation.csrf === "e2e-csrf-token")).toBe(true);
});

test("My Work filters by current assignee", async ({ page }) => {
  const state = await arrange(page, "CONTRIBUTOR");
  await page.goto("/app/acme/projects/SF");
  await page.getByRole("button", { name: "Bana atananlar" }).click();
  await expect(page.getByText("Size atanmış WorkItem yok.")).toBeVisible();
  state.setAssignee("actor", "actor@example.com");
  await page.reload();
  await page.getByRole("button", { name: "Bana atananlar" }).click();
  await expect(page.getByRole("link", { name: /SF-1.*Assignment flow/s })).toBeVisible();
});

test("contributor can self-assign but cannot manage another assignee", async ({ page }) => {
  const state = await arrange(page, "CONTRIBUTOR");
  await page.goto("/app/acme/work-items/SF-1");
  const assignment = page.getByRole("region", { name: "Assignment" });
  await expect(assignment.getByLabel("Assignee")).toHaveCount(0);
  await assignment.getByRole("button", { name: "Kendime ata" }).click();
  await assignment.getByRole("button", { name: "Atamayı kaldır" }).click();
  state.setAssignee("target", "target@example.com");
  await page.reload();
  await expect(assignment.getByRole("button", { name: "Kendime ata" })).toBeVisible();
  await expect(assignment.getByRole("button", { name: "Atamayı kaldır" })).toHaveCount(0);
  await expect(assignment.getByText("target@example.com", { exact: true })).toBeVisible();
});

for (const { role, archived } of [{ role: "VIEWER", archived: false }, { role: "MANAGER", archived: true }] as const) {
  test(`${role} archived=${archived} sees read-only assignment`, async ({ page }) => {
    await arrange(page, role, archived);
    await page.goto("/app/acme/work-items/SF-1");
    const assignment = page.getByRole("region", { name: "Assignment" });
    await expect(assignment).toContainText("Unassigned");
    await expect(assignment.getByRole("button")).toHaveCount(0);
    await expect(assignment.getByRole("combobox")).toHaveCount(0);
  });
}
