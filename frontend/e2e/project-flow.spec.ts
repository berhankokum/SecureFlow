import { expect, test } from "@playwright/test";

const csrfCookie = "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict";
const organization = {
  id: "11111111-1111-1111-1111-111111111111",
  name: "ACME",
  slug: "acme",
  logoUrl: null,
  role: "OWNER",
  permissions: ["member.view", "project.create", "project.update", "project.archive"],
  createdAt: "2026-09-02T00:00:00Z",
  archivedAt: null,
};

test.beforeEach(async ({ page }) => {
  await page.route("**/api/v1/antiforgery/token", route => route.fulfill({ status: 204, headers: { "set-cookie": csrfCookie } }));
  await page.route("**/api/v1/me", route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ id: "owner", email: "owner@example.com", emailConfirmed: true }) }));
  await page.route("**/api/v1/organizations", route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([organization]) }));
});

test("owner creates a project with an immutable key and opens its settings", async ({ page }) => {
  let projects: object[] = [];
  let csrfHeader: string | undefined;
  await page.route(`**/api/v1/organizations/${organization.id}/projects`, async route => {
    if (route.request().method() === "POST") {
      csrfHeader = route.request().headers()["x-csrf-token"];
      const payload = route.request().postDataJSON();
      const project = { id: "project-1", organizationId: organization.id, ...payload, createdBy: "owner", createdAt: "2026-09-02T00:00:00Z", updatedAt: "2026-09-02T00:00:00Z", archivedAt: null, projectRole: "MANAGER", permissions: ["project.update", "project.archive"] };
      projects = [project];
      await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(project) });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(projects) });
  });
  await page.route("**/api/v1/projects/project-1", route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ id: "project-1", organizationId: organization.id, key: "WEB", name: "Web Platform", description: "Frontend", createdBy: "owner", createdAt: "2026-09-02T00:00:00Z", updatedAt: "2026-09-02T00:00:00Z", archivedAt: null, projectRole: "MANAGER", permissions: ["project.update", "project.archive"], members: [{ userId: "owner", email: "owner@example.com", role: "MANAGER" }] }) }));
  await page.route(`**/api/v1/organizations/${organization.id}/members`, route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));

  await page.goto("/app/acme/projects");
  await page.getByRole("button", { name: "Project oluştur" }).click();
  await page.getByLabel("Project key").fill("web");
  await page.getByLabel("Project adı").fill("Web Platform");
  await page.getByLabel(/Açıklama/).fill("Frontend");
  await page.getByRole("button", { name: "Project oluştur" }).last().click();
  const projectLink = page.getByRole("link", { name: /WEB Web Platform/ });
  await expect(projectLink).toBeVisible();
  await projectLink.click();
  await expect(page).toHaveURL(/\/app\/acme\/projects\/WEB$/);
  await expect(page.getByRole("heading", { name: "Web Platform" })).toBeVisible();
  await page.getByRole("button", { name: "Ayarlar" }).click();
  await expect(page.getByText("Project key immutable: WEB")).toBeVisible();
  expect(csrfHeader).toBe("e2e-csrf-token");
});
