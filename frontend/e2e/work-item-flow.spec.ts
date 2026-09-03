import { expect, test } from "@playwright/test";

const csrfCookie = "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict";
const organization = {
  id: "11111111-1111-1111-1111-111111111111",
  name: "ACME",
  slug: "acme",
  logoUrl: null,
  role: "OWNER",
  permissions: ["member.view", "project.create", "workitem.create", "workitem.update", "workitem.archive"],
  createdAt: "2026-09-02T00:00:00Z",
  archivedAt: null,
};
const project = {
  id: "project-1", organizationId: organization.id, key: "SF", name: "SecureFlow", description: "Delivery",
  createdBy: "owner", createdAt: "2026-09-02T00:00:00Z", updatedAt: "2026-09-02T00:00:00Z",
  archivedAt: null, projectRole: "MANAGER", permissions: ["project.update", "project.archive", "workitem.create", "workitem.update", "workitem.archive"],
};

test.beforeEach(async ({ page }) => {
  await page.route("**/api/v1/projects/project-1/labels", route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route("**/api/v1/antiforgery/token", route => route.fulfill({ status: 204, headers: { "set-cookie": csrfCookie } }));
  await page.route("**/api/v1/me", route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ id: "owner", email: "owner@example.com", emailConfirmed: true }) }));
  await page.route("**/api/v1/organizations", route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([organization]) }));
  await page.route(`**/api/v1/organizations/${organization.id}/projects`, route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([project]) }));
  await page.route("**/api/v1/projects/project-1", route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ ...project, members: [{ userId: "owner", email: "owner@example.com", role: "MANAGER" }] }) }));
  await page.route(`**/api/v1/organizations/${organization.id}/members`, route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
});

test("manager creates, opens and edits a WorkItem", async ({ page }) => {
  let workItems: object[] = [];
  let csrfHeader: string | undefined;
  await page.route("**/api/v1/projects/project-1/work-items", async route => {
    if (route.request().method() === "POST") {
      csrfHeader = route.request().headers()["x-csrf-token"];
      const payload = route.request().postDataJSON();
      const workItem = { id: "workitem-1", organizationId: organization.id, projectId: project.id, projectKey: "SF", number: 1, key: "SF-1", ...payload, reporterId: "owner", reporterEmail: "owner@example.com", createdAt: "2026-09-02T00:00:00Z", updatedAt: "2026-09-02T00:00:00Z", archivedAt: null, permissions: project.permissions, labels: [] };
      workItems = [workItem];
      await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(workItem) });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(workItems) });
  });
  await page.route("**/api/v1/work-items/workitem-1", async route => {
    if (route.request().method() === "PATCH") {
      const current = workItems[0] as Record<string, unknown>;
      workItems = [{ ...current, ...route.request().postDataJSON(), updatedAt: "2026-09-02T01:00:00Z" }];
      await route.fulfill({ status: 204 });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(workItems[0]) });
  });

  await page.goto("/app/acme/projects/SF");
  await page.getByRole("button", { name: "WorkItem oluştur" }).click();
  await page.selectOption("#create-workitem-type", "BUG");
  await page.getByLabel("Başlık").fill("Tenant isolation");
  await page.selectOption("#create-workitem-priority", "CRITICAL");
  await page.getByRole("button", { name: "WorkItem oluştur" }).last().click();
  const workItemLink = page.getByRole("link", { name: /SF-1.*Tenant isolation/s });
  await expect(workItemLink).toBeVisible();
  await workItemLink.click();
  await expect(page).toHaveURL(/\/app\/acme\/work-items\/SF-1$/);
  await expect(page.getByRole("heading", { name: "Tenant isolation" })).toBeVisible();
  await page.getByRole("button", { name: "Düzenle" }).click();
  await page.getByLabel("Başlık").fill("Tenant boundary fixed");
  await page.getByRole("button", { name: "Kaydet" }).click();
  await expect(page.getByRole("heading", { name: "Tenant boundary fixed" })).toBeVisible();
  expect(csrfHeader).toBe("e2e-csrf-token");
});
