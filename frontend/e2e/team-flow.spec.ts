import { expect, test } from "@playwright/test";

const csrfCookie = "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict";
const organization = {
  id: "11111111-1111-1111-1111-111111111111",
  name: "ACME",
  slug: "acme",
  logoUrl: null,
  role: "OWNER",
  permissions: ["member.view", "team.create", "team.update", "team.archive", "team.member.manage"],
  createdAt: "2026-09-02T00:00:00Z",
  archivedAt: null,
};

test.beforeEach(async ({ page }) => {
  await page.route("**/api/v1/antiforgery/token", route =>
    route.fulfill({ status: 204, headers: { "set-cookie": csrfCookie } }));
  await page.route("**/api/v1/me", route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ id: "owner", email: "owner@example.com", emailConfirmed: true }) }));
  await page.route("**/api/v1/organizations", route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([organization]) }));
});

test("owner creates a team and opens its detail", async ({ page }) => {
  let teams: object[] = [];
  let csrfHeader: string | undefined;
  await page.route(`**/api/v1/organizations/${organization.id}/teams`, async route => {
    if (route.request().method() === "POST") {
      csrfHeader = route.request().headers()["x-csrf-token"];
      const payload = route.request().postDataJSON();
      const team = { id: "team-1", organizationId: organization.id, ...payload, memberCount: 0, createdAt: "2026-09-02T00:00:00Z", updatedAt: "2026-09-02T00:00:00Z", archivedAt: null };
      teams = [team];
      await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(team) });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(teams) });
  });
  await page.route("**/api/v1/teams/team-1", route => route.fulfill({
    status: 200,
    contentType: "application/json",
    body: JSON.stringify({ id: "team-1", organizationId: organization.id, name: "Security", description: "Security operations", createdAt: "2026-09-02T00:00:00Z", updatedAt: "2026-09-02T00:00:00Z", archivedAt: null, members: [] }),
  }));
  await page.route(`**/api/v1/organizations/${organization.id}/members`, route =>
    route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));

  await page.goto("/app/acme/teams");
  await page.getByRole("button", { name: "Team oluştur" }).click();
  await page.getByLabel("Team adı").fill("Security");
  await page.getByLabel(/Açıklama/).fill("Security operations");
  await page.getByRole("button", { name: "Team oluştur" }).last().click();
  const teamLink = page.getByRole("link", { name: /Security/ });
  await expect(teamLink).toBeVisible();
  await teamLink.click();
  await expect(page).toHaveURL(/\/app\/acme\/teams\/team-1$/);
  await expect(page.getByRole("heading", { name: "Security" })).toBeVisible();
  expect(csrfHeader).toBe("e2e-csrf-token");
});
