import { expect, test, type Page } from "@playwright/test";

const time = "2026-09-04T00:00:00Z";
const orgId = "11111111-1111-1111-1111-111111111111";
const states = [
  { id: "backlog", name: "BACKLOG", category: "TODO", position: 0 },
  { id: "todo", name: "TODO", category: "TODO", position: 1 },
  { id: "review", name: "IN_REVIEW", category: "IN_PROGRESS", position: 2 },
  { id: "done", name: "DONE", category: "DONE", position: 3 },
];
const transitions = [
  { id: "next", fromStateId: "backlog", toStateId: "todo", requiredPermission: null },
  { id: "approve", fromStateId: "review", toStateId: "done", requiredPermission: "workitem.approve" },
];

async function arrange(
  page: Page,
  options: { role?: "MANAGER" | "CONTRIBUTOR" | "VIEWER"; state?: "backlog" | "review"; archived?: boolean; reject?: boolean } = {},
) {
  const role = options.role ?? "MANAGER";
  const actorPermissions = role === "MANAGER"
    ? ["workitem.transition", "workitem.approve"]
    : role === "CONTRIBUTOR" ? ["workitem.transition"] : [];
  const organization = { id: orgId, name: "ACME", slug: "acme", role: "MEMBER", permissions: ["member.view"], createdAt: time, archivedAt: null };
  const project = {
    id: "project-1", organizationId: orgId, key: "SF", name: "SecureFlow", description: null,
    createdBy: "actor", createdAt: time, updatedAt: time, archivedAt: options.archived ? time : null,
    projectRole: role, permissions: actorPermissions, members: [],
  };
  let currentState = states.find(state => state.id === (options.state ?? "backlog"))!;
  const workItem = () => ({
    id: "item-1", organizationId: orgId, projectId: project.id, projectKey: "SF", number: 1, key: "SF-1",
    type: "TASK", title: "Kanban transition", description: null, priority: "HIGH",
    workflowStateId: currentState.id, workflowStateName: currentState.name, workflowStateCategory: currentState.category,
    reporterId: "actor", reporterEmail: "actor@example.com", assigneeId: null, assigneeEmail: null,
    dueDate: null, createdAt: time, updatedAt: time, archivedAt: null, permissions: actorPermissions, labels: [],
  });
  const requests: { path: string; csrf?: string }[] = [];

  await page.route("**/api/v1/**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
    if (path.endsWith("/antiforgery/token")) return route.fulfill({ status: 204, headers: { "set-cookie": "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict" } });
    if (path === "/api/v1/me") return json({ id: "actor", email: "actor@example.com", emailConfirmed: true });
    if (path === "/api/v1/organizations") return json([organization]);
    if (path === `/api/v1/organizations/${orgId}/projects`) return json([{ ...project, members: undefined }]);
    if (path === "/api/v1/projects/project-1") return json(project);
    if (path === "/api/v1/projects/project-1/work-items") return json([workItem()]);
    if (path === "/api/v1/projects/project-1/workflow") return json({ id: "workflow", organizationId: orgId, name: "DEFAULT", states, transitions });
    if (path === "/api/v1/projects/project-1/labels") return json([]);
    if (path === "/api/v1/work-items/item-1/transition" && method === "POST") {
      requests.push({ path, csrf: request.headers()["x-csrf-token"] });
      if (options.reject) return json({ title: "Geçersiz workflow transition." }, 409);
      currentState = states.find(state => state.id === request.postDataJSON().toStateId)!;
      return route.fulfill({ status: 204 });
    }
    return json({ title: "Unexpected test request" }, 404);
  });
  return requests;
}

test("manager moves a card optimistically through the transition API", async ({ page }) => {
  const requests = await arrange(page);
  await page.goto("/app/acme/projects/SF");
  await page.getByRole("button", { name: "Kanban Board" }).click();
  await page.getByLabel("SF-1 hedef state").selectOption("todo");

  await expect(page.getByRole("region", { name: "TODO kolonu" }).getByText("Kanban transition")).toBeVisible();
  expect(requests).toEqual([{ path: "/api/v1/work-items/item-1/transition", csrf: "e2e-csrf-token" }]);
});

test("rejected transition rolls the card back and displays the backend error", async ({ page }) => {
  await arrange(page, { reject: true });
  await page.goto("/app/acme/projects/SF");
  await page.getByRole("button", { name: "Kanban Board" }).click();
  await page.getByLabel("SF-1 hedef state").selectOption("todo");

  await expect(page.getByRole("alert")).toContainText("Geçersiz workflow transition.");
  await expect(page.getByRole("region", { name: "BACKLOG kolonu" }).getByText("Kanban transition")).toBeVisible();
});

for (const options of [
  { role: "CONTRIBUTOR" as const, state: "review" as const, archived: false },
  { role: "VIEWER" as const, state: "backlog" as const, archived: false },
  { role: "MANAGER" as const, state: "backlog" as const, archived: true },
]) {
  test(`${options.role} state=${options.state} archived=${options.archived} sees permission-aware board`, async ({ page }) => {
    await arrange(page, options);
    await page.goto("/app/acme/projects/SF");
    await page.getByRole("button", { name: "Kanban Board" }).click();
    await expect(page.getByText("Kanban transition")).toBeVisible();
    await expect(page.getByLabel("SF-1 hedef state")).toHaveCount(0);
  });
}

test("mobile board scroll stays inside its region and keyboard transition remains available", async ({ page }) => {
  const requests = await arrange(page);
  await page.setViewportSize({ width: 320, height: 700 });
  await page.goto("/app/acme/projects/SF");
  await page.getByRole("button", { name: "Kanban Board" }).click();
  const board = page.getByRole("region", { name: "Kanban board", exact: true });
  await expect(board.getByText("Kanban transition")).toBeVisible();
  for (const width of [320, 390, 768, 1280]) {
    await page.setViewportSize({ width, height: 700 });
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await expect(board).toHaveCSS("overflow-x", "auto");
  }
  const target = page.getByLabel("SF-1 hedef state");
  await target.focus();
  await target.press("ArrowDown");
  await expect(page.getByRole("region", { name: "TODO kolonu" }).getByText("Kanban transition")).toBeVisible();
  expect(requests).toHaveLength(1);
});
