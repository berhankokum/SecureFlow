import { expect, test } from "@playwright/test";

const csrfCookie = "XSRF-TOKEN=e2e-csrf-token; Path=/; Secure; SameSite=Strict";
const organization = {
  id: "11111111-1111-1111-1111-111111111111",
  name: "ACME",
  slug: "acme",
  logoUrl: null,
  role: "OWNER",
  permissions: [
    "organization.manage",
    "member.view",
    "member.invite",
    "member.remove",
    "member.role.change",
  ],
  createdAt: "2026-09-01T00:00:00Z",
  archivedAt: null,
};
const owner = {
  id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  email: "owner@example.com",
  emailConfirmed: true,
};

test.beforeEach(async ({ page }) => {
  await page.route("**/api/v1/antiforgery/token", async (route) => {
    await route.fulfill({ status: 204, headers: { "set-cookie": csrfCookie } });
  });
  await page.route("**/api/v1/me", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(owner) });
  });
  await page.route("**/api/v1/organizations", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([organization]) });
  });
});

test("owner invites a member and sees the pending invitation", async ({ page }) => {
  const members = [{
    id: "m1",
    userId: owner.id,
    email: owner.email,
    role: "OWNER",
    joinedAt: "2026-09-01T00:00:00Z",
    invitedBy: null,
  }];
  let invitations: object[] = [];
  let csrfHeader: string | undefined;

  await page.route(`**/api/v1/organizations/${organization.id}/members`, async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(members) });
  });
  await page.route(`**/api/v1/organizations/${organization.id}/invitations`, async (route) => {
    if (route.request().method() === "POST") {
      csrfHeader = route.request().headers()["x-csrf-token"];
      const payload = route.request().postDataJSON();
      const invitation = {
        id: "i1",
        email: payload.email,
        role: payload.role,
        expiresAt: "2026-09-04T00:00:00Z",
        createdAt: "2026-09-01T00:00:00Z",
        status: "PENDING",
      };
      invitations = [invitation];
      await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(invitation) });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(invitations) });
  });

  await page.goto("/app/acme/members");
  await page.getByRole("button", { name: "Üye davet et" }).click();
  await page.getByLabel("E-posta").fill("new.member@example.com");
  await page.getByRole("button", { name: "Davet gönder" }).click();

  await expect(page.getByText("new.member@example.com")).toBeVisible();
  expect(csrfHeader).toBe("e2e-csrf-token");
});

test("invited user accepts a valid invitation and enters the members page", async ({ page }) => {
  const token = "abcdefghijklmnopqrstuvwxyzABCDEFGH123456789";
  const invited = { ...owner, id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", email: "member@example.com" };
  await page.unroute("**/api/v1/me");
  await page.unroute("**/api/v1/organizations");
  await page.route("**/api/v1/me", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(invited) });
  });
  await page.route("**/api/v1/organizations", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([{ ...organization, role: "MEMBER", permissions: ["member.view"] }]),
    });
  });
  await page.route(`**/api/v1/invitations/${token}`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        id: "i1",
        organizationId: organization.id,
        organizationName: organization.name,
        organizationSlug: organization.slug,
        email: invited.email,
        role: "MEMBER",
        expiresAt: "2026-09-04T00:00:00Z",
        status: "PENDING",
      }),
    });
  });
  await page.route(`**/api/v1/invitations/${token}/accept`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        organizationId: organization.id,
        organizationName: organization.name,
        organizationSlug: organization.slug,
        role: "MEMBER",
      }),
    });
  });
  await page.route(`**/api/v1/organizations/${organization.id}/members`, async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
  });

  await page.goto(`/invitations/${token}`);
  await page.getByRole("button", { name: "Invitation'ı kabul et" }).click();

  await expect(page).toHaveURL(/\/app\/acme\/members$/);
  await expect(page.getByRole("heading", { name: "Üyeler" })).toBeVisible();
});
